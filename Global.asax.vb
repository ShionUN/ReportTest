
Imports System.IO

Public Class Global_asax
    Inherits HttpApplication

    Sub Application_Start(sender As Object, e As EventArgs)
        ' アプリケーションの起動時に呼び出されます
        'Try
        '    ' ClosedXMLの事前初期化
        '    WarmUpClosedXml()

        'Catch ex As Exception
        '    Trace.TraceError("ClosedXML WarmUp Error: " & ex.ToString())
        'End Try

    End Sub

    Private Sub WarmUpClosedXml()

        Dim templatePath As String = Server.MapPath("~/ExcelTemplates/ABC10001.xlsx")

        Using wb As New ClosedXML.Excel.XLWorkbook(templatePath)

            Using ms As New MemoryStream()
                wb.SaveAs(ms)
            End Using

        End Using

    End Sub
End Class