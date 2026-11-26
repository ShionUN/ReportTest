Imports System
Imports System.IO
Imports System.Web
Imports System.Web.SessionState
Imports System.Collections.Generic

Public Class Download
    Implements IHttpHandler
    Implements IReadOnlySessionState

    Public Sub ProcessRequest(context As HttpContext) _
        Implements IHttpHandler.ProcessRequest

        Dim request = context.Request
        Dim response = context.Response

        Dim batchId As String = request.QueryString("batch")
        Dim token As String = request.QueryString("token")

        ' パラメータチェック
        Dim id As Guid

        If Not Guid.TryParseExact(batchId, "N", id) OrElse
           Not Guid.TryParseExact(token, "N", id) Then

            response.StatusCode = 400
            Return

        End If

        ' キャッシュを無効化
        response.Cache.SetCacheability(HttpCacheability.NoCache)
        response.Cache.SetNoStore()

        ' Sessionからダウンロード情報を取得
        Dim files = TryCast(
            context.Session("Download_" & batchId),
            Dictionary(Of String, String))

        Dim fileName As String = Nothing

        ' ダウンロード権限チェック
        If files Is Nothing OrElse
           Not files.TryGetValue(token, fileName) Then

            SetDownloadCookie(context, token, "error")
            response.StatusCode = 403
            Return

        End If

        ' 一時ファイルのパスを取得
        Dim filePath As String = context.Server.MapPath("~/App_Data/Downloads/" & batchId & "/" & token)

        ' ファイル存在チェック
        If Not File.Exists(filePath) Then

            SetDownloadCookie(context, token, "error")
            response.StatusCode = 404
            Return

        End If

        ' ダウンロードレスポンスの設定
        response.Clear()
        response.BufferOutput = False

        response.ContentType =
            MimeMapping.GetMimeMapping(fileName)

        response.AddHeader("Content-Disposition", "attachment; filename*=UTF-8''" & Uri.EscapeDataString(fileName))

        response.AddHeader("Content-Length", New FileInfo(filePath).Length.ToString())

        ' ダウンロード開始通知
        SetDownloadCookie(context, token, "ready")

        ' ファイル送信
        response.TransmitFile(filePath)

        context.ApplicationInstance.CompleteRequest()

    End Sub

    ' ダウンロード状態通知用Cookie
    Private Sub SetDownloadCookie(
        context As HttpContext,
        token As String,
        value As String)

        Dim cookie As New HttpCookie(
            "download_" & token, value)

        cookie.Path = "/"
        cookie.HttpOnly = False
        cookie.Secure = context.Request.IsSecureConnection
        cookie.Expires = DateTime.Now.AddMinutes(5)

        context.Response.Cookies.Add(cookie)

    End Sub

    Public ReadOnly Property IsReusable As Boolean _
        Implements IHttpHandler.IsReusable

        Get
            Return False
        End Get

    End Property

End Class