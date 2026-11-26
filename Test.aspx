<%@ Page Language="vb" AutoEventWireup="false" CodeBehind="Test.aspx.vb" Inherits="WebT1.Test" %>

<!DOCTYPE html>

<html lang="ja">
<head runat="server">
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title>帳票出力</title>
    <link rel="stylesheet" href="css/style.css" />
    <script src="js/core.js"></script>
</head>
<body>
    <form id="form1" runat="server">
        <div class="page-layout">
            <div class="header">
                <div class="header-inner">
                    <h1>帳票出力</h1>
                </div>
            </div>

            <div class="main">
                <asp:Panel ID="pnlActionBar" runat="server" CssClass="action-bar">
                    <asp:Button ID="btnDownload" runat="server" Text="ダウンロード" CssClass="download-button"
                        OnClientClick="return preventDoubleDownload();" OnClick="btnDownload_Click" ClientIDMode="Static" />

                    <div id="selectionSummary" class="selection-summary" aria-live="polite">
                        ダウンロードする帳票を選択してください。
                    </div>

                    <asp:Button ID="btnExecProc" runat="server" Text="ストアド実行" CssClass="stored-button" OnClick="btnExecProc_Click" ClientIDMode="Static" />
                </asp:Panel>

                <asp:Label ID="lblMessage" runat="server" CssClass="message" ClientIDMode="Static" />

                <asp:Panel ID="pnlReportList" runat="server" CssClass="report-list">
                    <asp:GridView ID="gvReports" runat="server" AutoGenerateColumns="False" CssClass="report-grid" GridLines="None"
                        DataKeyNames="処理ID" OnRowDataBound="gvReports_RowDataBound" ClientIDMode="Static">
                        <Columns>
                            <asp:TemplateField HeaderText="No">
                                <ItemTemplate><%# Container.DataItemIndex + 1 %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="選択">
                                <ItemTemplate>
                                    <asp:CheckBox ID="chkSelect" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="タイトル" HeaderText="タイトル" />
                            <asp:TemplateField HeaderText="抽出条件">
                                <ItemTemplate>
                                    <asp:PlaceHolder ID="phConditions" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="作成データ">
                                <ItemTemplate>
                                    <span class="output-filename" data-filename='<%# Eval("ファイル") %>'></span>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </asp:Panel>
            </div>
        </div>
    </form>
</body>
</html>
