Imports System.Data.SqlClient

Public Class ReportRepository

    Private ReadOnly _connectionString As String

    Public Sub New()
        _connectionString = ConfigurationManager.ConnectionStrings("ReportDB").ConnectionString
    End Sub

    Public Function GetReports() As List(Of ReportDef)

        Dim reports As New List(Of ReportDef)

        Dim sql As String =
        "SELECT " &
        " MK.[処理ID], " &
        " MK.[ソート用通番], " &
        " MK.[タイトル], " &
        " MK.[ファイル], " &
        " MK.[複数条件区分], " &
        " MK.[EXCEL種類], " &
        " MK.[主管部署], " &
        " MK.[廃止区分], " &
        " DK.[テーブル名], " &
        " DK.[ソートキー] " &
        "FROM dbo.T_MK AS MK " &
        "INNER JOIN dbo.T_DK AS DK " &
        " ON MK.[処理ID] = DK.[処理ID] " &
        "WHERE MK.[廃止区分] = '0' " &
        " AND EXISTS ( " &
        "   SELECT 1 FROM dbo.T_KK AS KK " &
        "   WHERE KK.[処理ID] = MK.[処理ID] " &
        "     AND KK.[部署コード] = @Department " &
        " ) " &
        "ORDER BY MK.[ソート用通番], MK.[処理ID]"

        Using con As New SqlConnection(_connectionString)
            Using cmd As New SqlCommand(sql, con)

                ' 今回はテスト用に固定
                cmd.Parameters.Add("@Department", SqlDbType.VarChar, 3).Value = "120"

                con.Open()

                Using reader As SqlDataReader = cmd.ExecuteReader()

                    While reader.Read()

                        Dim report As New ReportDef With {
                        .Id = Convert.ToString(reader("処理ID")),
                        .OrderNo = Convert.ToInt32(reader("ソート用通番")),
                        .Title = Convert.ToString(reader("タイトル")),
                        .FileName = Convert.ToString(reader("ファイル")),
                        .Multiple = Convert.ToString(reader("複数条件区分")) = "1",
                        .ExcelType = If(
                            reader.IsDBNull(reader.GetOrdinal("EXCEL種類")),
                            CType(Nothing, Integer?),
                            CType(Convert.ToInt32(reader("EXCEL種類")), Integer?)
                        ),
                        .ViewName = Convert.ToString(reader("テーブル名")),
                        .SortKey = Convert.ToString(reader("ソートキー")),
                        .Department = "120",
                        .OwnerDepartment = If(
                            reader.IsDBNull(reader.GetOrdinal("主管部署")),
                            0,
                            Convert.ToInt32(reader("主管部署"))
                        ),
                        .Discontinued = Convert.ToString(reader("廃止区分")) = "1"
                    }

                        reports.Add(report)

                    End While

                End Using

            End Using
        End Using

        Return reports

    End Function

    Public Function GetConditionDefinitions() As List(Of ConditionDef)

        Dim result As New List(Of ConditionDef)

        Dim sql As String =
        "SELECT [処理ID], [No], [項目名], [指定区分], " &
        "       [必須区分], [表示順], [桁数] " &
        "FROM dbo.T_DS " &
        "ORDER BY [処理ID], [No]"

        Using con As New SqlConnection(_connectionString)
            Using cmd As New SqlCommand(sql, con)

                con.Open()

                Using reader As SqlDataReader = cmd.ExecuteReader()

                    While reader.Read()

                        result.Add(New ConditionDef With {
                        .ReportId = Convert.ToString(reader("処理ID")),
                        .No = Convert.ToInt32(reader("No")),
                        .FieldName = Convert.ToString(reader("項目名")),
                        .InputType = Convert.ToInt32(reader("指定区分")),
                        .Required = Convert.ToInt32(reader("必須区分")) = 1,
                        .SortDirection = If(
                            reader.IsDBNull(reader.GetOrdinal("表示順")),
                            0,
                            Convert.ToInt32(reader("表示順"))
                        ),
                        .Length = If(
                            reader.IsDBNull(reader.GetOrdinal("桁数")),
                            0,
                            Convert.ToInt32(reader("桁数"))
                        )
                    })

                    End While

                End Using

            End Using
        End Using

        Return result

    End Function

    Public Function GetDistinctValues(report As ReportDef, def As ConditionDef) As List(Of String)

        Dim result As New List(Of String)

        Dim sql As String

        If def.FieldName = "基準日" Then

            sql =
            "SELECT DISTINCT [基準日] " &
            "FROM dbo.T_RD " &
            "WHERE [処理ID] = @ReportId " &
            "ORDER BY [基準日] DESC"

        Else

            ' ビュー名・列名はパラメータ化できないため、
            ' DBの帳票定義から取得した値を識別子として扱う
            Dim viewName As String = QuoteSqlIdentifier(report.ViewName)

            Dim columnName As String = QuoteSqlIdentifier(def.FieldName)

            Dim direction As String = If(def.SortDirection = 2, "DESC", "ASC")

            sql =
            "SELECT DISTINCT " & columnName & " " &
            "FROM dbo." & viewName & " " &
            "WHERE " & columnName & " IS NOT NULL " &
            "ORDER BY " & columnName & " " & direction

        End If

        Using con As New SqlConnection(_connectionString)
            Using cmd As New SqlCommand(sql, con)

                If def.FieldName = "基準日" Then
                    cmd.Parameters.Add("@ReportId", SqlDbType.VarChar, 5).Value = report.Id
                End If

                con.Open()

                Using reader As SqlDataReader = cmd.ExecuteReader()

                    While reader.Read()

                        If Not reader.IsDBNull(0) Then
                            result.Add(Convert.ToString(reader(0)))
                        End If

                    End While

                End Using

            End Using
        End Using

        Return result

    End Function

    Public Function GetReportData(report As ReportDef, conditions As Dictionary(Of String, String), conditionDefs As List(Of ConditionDef)) As DataTable

        Dim dt As New DataTable()

        Dim sql As New System.Text.StringBuilder()

        sql.Append("SELECT * FROM dbo.")
        sql.Append(QuoteSqlIdentifier(report.ViewName))

        Dim whereParts As New List(Of String)

        Using con As New SqlConnection(_connectionString)
            Using cmd As New SqlCommand()

                cmd.Connection = con

                Dim index As Integer = 0

                For Each def In conditionDefs

                    Dim value As String = Nothing

                    If Not conditions.TryGetValue(def.FieldName, value) Then
                        Continue For
                    End If

                    If String.IsNullOrWhiteSpace(value) Then
                        Continue For
                    End If

                    Dim parameterName As String = "@p" & index.ToString()

                    whereParts.Add(QuoteSqlIdentifier(def.FieldName) & " = " & parameterName)

                    ' SQL Server側で列型に応じて変換
                    cmd.Parameters.Add(parameterName, SqlDbType.VarChar, Math.Max(value.Length, 1)).Value = value

                    index += 1

                Next

                If whereParts.Count > 0 Then
                    sql.Append(" WHERE ")
                    sql.Append(String.Join(" AND ", whereParts))
                End If

                ' ソートキーはDB管理者が登録した定義のみ使用する
                If Not String.IsNullOrWhiteSpace(report.SortKey) Then
                    sql.Append(" ")
                    sql.Append(report.SortKey)
                End If

                cmd.CommandText = sql.ToString()

                con.Open()

                Using adapter As New SqlDataAdapter(cmd)
                    adapter.Fill(dt)
                End Using

            End Using
        End Using

        Return dt

    End Function

    Private Function QuoteSqlIdentifier(value As String) As String

        If String.IsNullOrWhiteSpace(value) OrElse value.Length > 128 Then

            Throw New ArgumentException("SQL識別子が不正です。")

        End If

        Return "[" & value.Replace("]", "]]") & "]"

    End Function

End Class