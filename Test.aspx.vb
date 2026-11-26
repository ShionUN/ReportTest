Imports System.IO
Imports System.Web.Script.Serialization

Public Class Test
    Inherits System.Web.UI.Page

    Private _repository As ReportRepository

    Private ReadOnly Property Repository As ReportRepository
        Get
            If _repository Is Nothing Then
                _repository = New ReportRepository()
            End If

            Return _repository
        End Get
    End Property

    Private _reports As List(Of ReportDef)

    Private ReadOnly Property Reports As List(Of ReportDef)
        Get
            If _reports Is Nothing Then
                _reports = Repository.GetReports()
            End If

            Return _reports
        End Get
    End Property

    Private _conditionDefinitions As List(Of ConditionDef)

    Private ReadOnly Property ConditionDefinitions As List(Of ConditionDef)
        Get
            If _conditionDefinitions Is Nothing Then
                _conditionDefinitions = Repository.GetConditionDefinitions()
            End If

            Return _conditionDefinitions
        End Get
    End Property

    Protected Sub Page_Init(sender As Object, e As EventArgs) Handles Me.Init
        ' タイムアウト後の表示では帳票を初期化しない
        If Request.QueryString("timeout") = "1" Then
            Return
        End If

        ' PostBack時にSessionが失われていたら処理しない
        If IsPostBack AndAlso Session("ReportPageActive") Is Nothing Then

            Response.Redirect(Request.Url.AbsolutePath & "?timeout=1", True)
            Return

        End If

        ' 新規表示時は候補キャッシュを初期化
        If Not IsPostBack Then
            Session("ReportOptions") = New Dictionary(Of String, List(Of String))
            Session("ReportPageActive") = True
        End If

        ' Initで毎回同じ構成を生成し、ポストバック値の復元に間に合わせる。
        Dim dt As New DataTable()
        dt.Columns.Add("処理ID", GetType(String))
        dt.Columns.Add("タイトル", GetType(String))
        dt.Columns.Add("ファイル", GetType(String))

        For Each report In Reports.Where(Function(r) Not r.Discontinued).OrderBy(Function(r) r.OrderNo)
            dt.Rows.Add(report.Id, report.Title, report.FileName)
        Next

        gvReports.DataSource = dt
        gvReports.DataBind()
    End Sub

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        If Not IsPostBack AndAlso Request.QueryString("timeout") = "1" Then

            lblMessage.CssClass = "message error"
            lblMessage.Text =
                "セッションの有効期限が切れました。再度メニューからアクセスしてください。"

            pnlActionBar.Visible = False
            pnlReportList.Visible = False

            Return
        End If
    End Sub

    ' ダウンロード処理：全帳票生成後に個別ダウンロード
    Protected Sub btnDownload_Click(sender As Object, e As EventArgs)

        lblMessage.Text = String.Empty
        lblMessage.CssClass = "message"

        Dim selectedRows = GetSelectedRows()

        If selectedRows.Count = 0 Then
            ShowDownloadError("ダウンロードする帳票を選択してください。")
            Return
        End If

        Try
            ' 送信前に全帳票の条件とファイル名を確認する
            Dim targets As New List(Of DownloadTarget)
            Dim usedNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            For Each row As GridViewRow In selectedRows
                Dim reportId As String = Convert.ToString(gvReports.DataKeys(row.RowIndex).Value)
                Dim report = Reports.First(Function(r) r.Id = reportId)
                Dim conditions = GetSelectedConditions(row, report)

                If conditions Is Nothing Then Return

                Dim fileName As String = GetDownloadFileName(report, conditions)
                If Not usedNames.Add(fileName) Then
                    ShowDownloadError("ファイル名が重複しています：" & fileName)
                    Return
                End If

                targets.Add(New DownloadTarget With {
                    .Report = report,
                    .Conditions = conditions,
                    .FileName = fileName
                })
            Next

            PrepareDownloads(targets)

        Catch ex As Exception
            System.Diagnostics.Trace.TraceError(ex.ToString())
            ShowDownloadError("帳票の作成に失敗しました。")
        End Try

    End Sub

    ' ダウンロード対象（画面内でのみ使用）
    Private Class DownloadTarget
        Public Property Report As ReportDef
        Public Property Conditions As Dictionary(Of String, String)
        Public Property FileName As String
    End Class

    Private Function GetSelectedRows() As List(Of GridViewRow)
        Dim rows As New List(Of GridViewRow)

        For Each row As GridViewRow In gvReports.Rows
            Dim chk = TryCast(row.FindControl("chkSelect"), CheckBox)
            If chk IsNot Nothing AndAlso chk.Checked Then rows.Add(row)
        Next

        Return rows
    End Function

    Protected Sub gvReports_RowDataBound(sender As Object, e As GridViewRowEventArgs)

        If e.Row.RowType <> DataControlRowType.DataRow Then
            Return
        End If

        ' 対象帳票
        Dim reportId As String = Convert.ToString(DataBinder.Eval(e.Row.DataItem, "処理ID"))
        Dim report = Reports.First(Function(r) r.Id = reportId)

        ' チェックボックスのアクセシビリティ対応
        Dim chk = CType(e.Row.FindControl("chkSelect"), CheckBox)
        chk.InputAttributes("aria-label") = report.Title & "を選択"

        ' 抽出条件の配置先
        Dim ph = CType(e.Row.FindControl("phConditions"), PlaceHolder)

        Dim wrapper As New Panel With {
            .CssClass = "condition-list"
        }

        For Each def In GetConditions(report).OrderBy(Function(c) c.No)

            Dim controlId As String = "cond_" & def.No.ToString()

            ' 入力コントロールを生成
            Dim inputControl As WebControl = Nothing

            Select Case def.InputType

                Case 1
                    ' DropDownList
                    Dim ddl As New DropDownList With {
                        .ID = controlId,
                        .ClientIDMode = ClientIDMode.AutoID
                    }

                    For Each value As String In GetCachedDistinctValues(report, def)
                        ddl.Items.Add(New ListItem(FormatOption(def.FieldName, value), value))
                    Next

                    inputControl = ddl

                Case 2
                    ' TextBox
                    Dim tb As New TextBox With {
                        .ID = controlId,
                        .ClientIDMode = ClientIDMode.AutoID,
                        .MaxLength = def.Length,
                        .TextMode = TextBoxMode.Phone
                    }

                    inputControl = tb

                Case Else
                    Throw New InvalidOperationException("未対応の入力タイプです: " & def.InputType.ToString())

            End Select

            ' 条件項目
            Dim item As New Panel With {
                .CssClass = "condition-item"
            }

            ' ラベル
            Dim caption As New Label With {
                .CssClass = "condition-label",
                .AssociatedControlID = controlId,
                .Text = Server.HtmlEncode(def.FieldName) &
                If(def.Required, " <span class=""required"">*</span>", "")
            }

            item.Controls.Add(caption)
            item.Controls.Add(inputControl)

            wrapper.Controls.Add(item)

        Next

        ph.Controls.Add(wrapper)

    End Sub

    Private Function GetConditions(report As ReportDef) As List(Of ConditionDef)

        If Not report.Multiple Then

            Return New List(Of ConditionDef) From {
            New ConditionDef With {
                .ReportId = report.Id,
                .No = 1,
                .FieldName = "基準日",
                .InputType = 1,
                .Required = True,
                .SortDirection = 2,
                .Length = 0
            }
        }

        End If

        Return ConditionDefinitions.
            Where(Function(c) c.ReportId = report.Id).
            OrderBy(Function(c) c.No).
            ToList()

    End Function

    Private Function FormatOption(fieldName As String, value As String) As String
        If fieldName = "基準日" AndAlso value.Length = 8 Then
            Return value.Substring(0, 4) & "/" & value.Substring(4, 2) & "/" & value.Substring(6, 2)
        End If
        Return value
    End Function

    Private Function FindChild(parent As Control, id As String) As Control
        For Each child As Control In parent.Controls
            If child.ID = id Then Return child
            Dim found = FindChild(child, id)
            If found IsNot Nothing Then Return found
        Next
        Return Nothing
    End Function

    Private Function GetTemplatePath(report As ReportDef) As String

        If Not report.ExcelType.HasValue Then
            Throw New InvalidOperationException("CSV帳票にテンプレートはありません。")
        End If

        If Not report.ViewName.StartsWith("VW_", StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("ビュー名の形式が不正です。")
        End If

        Dim extension As String

        Select Case report.ExcelType.Value
            Case 1
                extension = ".xlsx"
            Case 2
                extension = ".xlsm"
            Case Else
                Throw New InvalidOperationException("未対応のEXCEL種類です。")
        End Select

        Dim templateName As String = report.ViewName.Substring(3) & extension

        Dim path As String = Server.MapPath("~/ExcelTemplates/" & templateName)

        If Not File.Exists(path) Then
            Throw New FileNotFoundException("Excelテンプレートが見つかりません。", path)
        End If

        Return path

    End Function


    Private Function GetDownloadFileName(report As ReportDef, conditions As Dictionary(Of String, String)) As String

        Dim fileName As New System.Text.StringBuilder()

        For Each def In GetConditions(report)

            If Not def.Required Then
                Continue For
            End If

            Dim value As String = Nothing

            If conditions.TryGetValue(def.FieldName, value) Then
                fileName.Append(If(value, ""))
            End If

        Next

        ' T_MK.ファイル
        fileName.Append(report.FileName)

        ' 拡張子
        If Not report.ExcelType.HasValue Then
            fileName.Append(".csv")

        ElseIf report.ExcelType.Value = 1 Then
            fileName.Append(".xlsx")

        ElseIf report.ExcelType.Value = 2 Then
            fileName.Append(".xlsm")

        Else
            Throw New InvalidOperationException("未対応のEXCEL種類です。")

        End If

        Return fileName.ToString()

    End Function


    Private Function ValidateCondition(def As ConditionDef, value As String) As String

        ' 必須チェック
        If def.Required AndAlso String.IsNullOrWhiteSpace(value) Then
            Return def.FieldName & "は必須です。"
        End If

        ' 未入力の任意項目はチェック不要
        If String.IsNullOrEmpty(value) Then
            Return Nothing
        End If

        ' TextBoxのみチェック
        If def.InputType = 2 Then

            ' 最大桁数チェック
            If value.Length > def.Length Then
                Return def.FieldName & "は" &
                   def.Length.ToString() & "桁以内で入力してください。"
            End If

            ' 半角数字のみ許可
            If Not System.Text.RegularExpressions.Regex.IsMatch(value, "^[0-9]+$") Then

                Return def.FieldName & "は半角数字で入力してください。"
            End If

        End If

        Return Nothing

    End Function

    Private Function GetCachedDistinctValues(report As ReportDef, def As ConditionDef) As List(Of String)

        Dim cache = TryCast(Session("ReportOptions"), Dictionary(Of String, List(Of String)))

        If cache Is Nothing Then
            cache = New Dictionary(Of String, List(Of String))
            Session("ReportOptions") = cache
        End If

        Dim key As String = report.Id & "_" & def.No

        If Not cache.ContainsKey(key) Then
            cache(key) = Repository.GetDistinctValues(report, def)
        End If

        Return cache(key)

    End Function

    Protected Sub btnExecProc_Click(sender As Object, e As EventArgs)

    End Sub


    Private Sub ShowDownloadError(message As String)

        lblMessage.CssClass = "message error"
        lblMessage.Text = message

    End Sub


    Private Function GetSelectedConditions(selectedRow As GridViewRow,
                                           report As ReportDef) As Dictionary(Of String, String)

        Dim ph = CType(selectedRow.FindControl("phConditions"), PlaceHolder)

        Dim conditions As New Dictionary(Of String, String)

        For Each def In GetConditions(report)

            Dim ctrl = FindChild(ph, "cond_" & def.No.ToString())

            Dim value As String = ""

            If TypeOf ctrl Is DropDownList Then
                value = CType(ctrl, DropDownList).SelectedValue

            ElseIf TypeOf ctrl Is TextBox Then
                value = CType(ctrl, TextBox).Text.Trim()

            End If

            Dim errorMessage As String = ValidateCondition(def, value)

            If errorMessage IsNot Nothing Then
                ShowDownloadError(report.Title & "：" & errorMessage)
                ctrl.Focus()
                Return Nothing

            End If

            conditions(def.FieldName) = value

        Next

        Return conditions

    End Function



    Private Function CreateReportFile(report As ReportDef,
                                      conditions As Dictionary(Of String, String),
                                      ms As MemoryStream) As Boolean

        ' DBから帳票データを取得
        Dim dt As DataTable =
        Repository.GetReportData(report, conditions, GetConditions(report))

        ' 0件チェック
        If dt.Rows.Count = 0 Then
            ShowDownloadError(report.Title & "：該当データがありません。")
            Return False

        End If

        ' CSV／Excel生成
        If Not report.ExcelType.HasValue Then
            CsvCommon.CreateCsv(dt, ms)

        Else
            Dim templatePath As String = GetTemplatePath(report)
            ExcelCommon.CreateExcel(templatePath, report.Title, conditions("基準日"), dt, ms)

        End If

        Return True

    End Function


    Private Sub PrepareDownloads(targets As List(Of DownloadTarget))

        Dim batchId As String = Guid.NewGuid().ToString("N")

        Dim directoryPath As String =
            Server.MapPath("~/App_Data/Downloads/" & batchId)

        Dim files As New Dictionary(Of String, String)
        Dim tokens As New List(Of String)

        Dim prepared As Boolean = False

        Try
            Directory.CreateDirectory(directoryPath)

            ' 帳票を1件ずつ生成
            For Each target In targets

                Dim token As String = Guid.NewGuid().ToString("N")

                Dim filePath As String =
                    Path.Combine(directoryPath, token)

                Using ms As New MemoryStream()

                    If Not CreateReportFile(
                        target.Report,
                        target.Conditions,
                        ms) Then

                        Return

                    End If

                    ms.Position = 0

                    Using fs As FileStream = File.Create(filePath)
                        ms.CopyTo(fs)
                    End Using

                End Using

                files.Add(token, target.FileName)
                tokens.Add(token)

            Next

            ' 全帳票の生成成功後にSessionへ登録
            Session("Download_" & batchId) = files

            ' ダウンロード開始
            Dim serializer As New JavaScriptSerializer()

            Dim script As String =
                "startDownloads(" &
                serializer.Serialize(batchId) & "," &
                serializer.Serialize(tokens) & ");"

            ClientScript.RegisterStartupScript(
                Me.GetType(),
                "startDownloads",
                script,
                True)

            prepared = True

        Finally

            ' 途中で失敗した場合は一時ファイルを削除
            If Not prepared Then

                Session.Remove("Download_" & batchId)

                If Directory.Exists(directoryPath) Then
                    Directory.Delete(directoryPath, True)
                End If

            End If

        End Try

    End Sub

End Class