Imports System.IO

Public NotInheritable Class CsvCommon

    Private Sub New()
    End Sub

    Public Shared Sub CreateCsv(dt As DataTable, output As Stream)

        Dim encoding As Encoding = Encoding.GetEncoding(932)

        Using writer As New StreamWriter(output, encoding, 1024, True)

            ' ヘッダー行
            Dim headers As New List(Of String)

            For Each col As DataColumn In dt.Columns
                headers.Add(EscapeCsv(col.ColumnName))
            Next

            writer.WriteLine(String.Join(",", headers))

            ' データ行
            For Each row As DataRow In dt.Rows

                Dim values As New List(Of String)

                For Each col As DataColumn In dt.Columns

                    Dim value As String =
                        If(row.IsNull(col), "",
                           Convert.ToString(row(col)))

                    values.Add(EscapeCsv(value))

                Next

                writer.WriteLine(String.Join(",", values))

            Next

            writer.Flush()

        End Using

    End Sub


    Private Shared Function EscapeCsv(value As String) As String

        If value Is Nothing Then value = ""

        Return """" & value.Replace("""", """""") & """"

    End Function


End Class
