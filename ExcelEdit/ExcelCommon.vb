
Imports System.IO
Imports ClosedXML.Excel

Public Class ExcelCommon

    Public Shared Sub CreateExcel(templatePath As String, reportTitle As String, baseDate As String, dt As DataTable, output As Stream)

        Using wb As New XLWorkbook(templatePath)

            Dim ws = wb.Worksheet(1)

            ' 固定セルへの値設定
            ws.Cell("B2").Value = reportTitle
            ws.Cell("D3").Value = baseDate

            ' 明細データ
            Const startRow As Integer = 6
            Const startCol As Integer = 1

            If dt.Rows.Count > 0 Then

                ' DataTableのデータ部分のみ挿入
                ws.Cell(startRow, startCol).InsertData(dt)

                Dim lastRow As Integer = startRow + dt.Rows.Count - 1

                Dim lastCol As Integer = startCol + dt.Columns.Count - 1

                Dim dataRange = ws.Range(startRow, startCol, lastRow, lastCol)

                ' 罫線は範囲にまとめて設定
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin

                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin

                ' 残高列の表示形式
                ws.Range(startRow, 4, lastRow, 4).Style.NumberFormat.Format = "#,##0"

            End If

            ' MemoryStreamなどへ保存
            wb.SaveAs(output)

        End Using

    End Sub

End Class
