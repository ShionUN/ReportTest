Imports System.IO

Public NotInheritable Class DownloadCommon

    Private Sub New()
    End Sub

    Public Shared Sub SendFile(response As HttpResponse, fileName As String, ms As MemoryStream)

        Dim encodedName As String = Uri.EscapeDataString(fileName)

        ' ファイル先頭に戻す
        ms.Position = 0

        ' レスポンス初期化
        response.Clear()

        ' ヘッダー設定
        response.ContentType = MimeMapping.GetMimeMapping(fileName)
        response.AddHeader("Content-Disposition", "attachment; filename=""download""; filename*=UTF-8''" & encodedName)
        response.AddHeader("Content-Length", ms.Length.ToString())

        ' ファイル送信
        ms.CopyTo(response.OutputStream)

        response.Flush()

        ' 後続のASPXのHTML出力を抑制
        response.SuppressContent = True

        ' HTTPリクエストの終了処理
        HttpContext.Current.ApplicationInstance.CompleteRequest()

    End Sub

End Class
