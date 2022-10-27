using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Generic;
using JSFW.VS.Extensibility.Cmds.Controls;

using EnvDTE;
using EnvDTE80;
using System.Linq;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Folding;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;

namespace JSFW.VS.Extensibility.Cmds
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class VariableUsingView
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("5bee326a-3399-4db3-94df-0e82488b7216");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        /// <summary>
        /// Initializes a new instance of the <see cref="VariableUsingView"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private VariableUsingView(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(this.MenuItemCallback, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static VariableUsingView Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new VariableUsingView(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            //string message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.GetType().FullName);
            //string title = "VariableUsingView";

            //// Show a message box to prove we were here
            //VsShellUtilities.ShowMessageBox(
            //    this.ServiceProvider,
            //    message,
            //    title,
            //    OLEMSGICON.OLEMSGICON_INFO,
            //    OLEMSGBUTTON.OLEMSGBUTTON_OK,
            //    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            

            Results.Clear();
            currentLine = -1;

            // 변수명 get! 
            string varibable = GetSelection(false); // 기본 선택된 문자열

            if (string.IsNullOrEmpty(varibable)) return;

            if (0 <= varibable.Trim().IndexOf(' ')) return;

            // 소스 전체 get!
            string text = GetSelection(true);

            if (string.IsNullOrEmpty(text.Trim())) return;
             

            if (GetLanguage() == Language_Basic)
            {
                VBReSearch( varibable, text);

                if (CurrentSelection != null && 0 <= currentLine)
                    CurrentSelection.GotoLine(currentLine, false);
            }
            else if (GetLanguage() == Language_CSharp)
            {
                SourceList = RemoveComment(text);
                ReSearch(varibable, SourceList); 

                if (CurrentSelection != null && 0 <= currentLine)
                    CurrentSelection.GotoLine(currentLine, false); 
            }
            else
            {
                VsShellUtilities.ShowMessageBox(
                   this.ServiceProvider,
                   GetLanguage(),
                   "지원하지 않는 언어입니다.",
                   OLEMSGICON.OLEMSGICON_INFO,
                   OLEMSGBUTTON.OLEMSGBUTTON_OK,
                   OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST); 
            }
        }

        readonly string Language_Basic = "Basic";
        readonly string Language_CSharp = "CSharp";

        #region VB

        private string GetLanguage()
        {
            string language = "";
            DTE2 _applicationObject = ServiceProvider.GetService(typeof(DTE)) as DTE2;
            //Check active document
            if (_applicationObject.ActiveDocument != null)
            {
                //Get active document
                EnvDTE.TextDocument objTextDocument = (EnvDTE.TextDocument)_applicationObject.ActiveDocument.Object("");
                language = objTextDocument?.Language ?? "";
            }
            return language;
        }

        List<bool> FindUsingVariableLines = new List<bool>();

        List<SyntaxNode> lst = new List<SyntaxNode>();

        private void VBReSearch(string varibable, string text)
        {
            SortedDictionary<int, string> textResultList = new SortedDictionary<int, string>();
            SyntaxTree tree = VisualBasicSyntaxTree.ParseText(text);

            lst.Clear();
            FindUsingVariableLines.Clear();
            FindUsingVariableLines.AddRange(Enumerable.Range(0, text.Split('\n').Length).Select(s => false).ToArray());

            var root = tree.GetCompilationUnitRoot();
            GetNode(root, varibable);

            foreach (var node in lst)
            {
                SetUsingLineParent(node);
            }

            Results.Clear();

            string[] sourceLines = text.Replace("\r", "").Split('\n');

            for (int line = 0; line < FindUsingVariableLines.Count; line++)
            {
                if (FindUsingVariableLines[line])
                {
                    Results.Add(line, 1);
                    textResultList.Add(line, sourceLines[line]);
                }
            }

            SourceList = textResultList;

            ShowToolWindow(textResultList.Values.ToArray(), varibable, Language_Basic);

        }

        public void GetNode(SyntaxNode node, string variable)
        {
            if (node == null) return;

            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.VariableDeclaratorSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.VariableDeclaratorSyntax;

                string txt = "" + _var.Names[0].Identifier.ValueText;// + _var.ValueText;
                if (txt == variable)
                {
                    var location = node.GetLocation();
                    int lineNo = location.GetMappedLineSpan().StartLinePosition.Line;
                    if (LineContains(lineNo) == false)
                    {
                        lst.Add(node);
                    }
                }
            }
            else if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.SimpleNameSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.SimpleNameSyntax;

                string txt = "" + _var.Identifier.ValueText;
                if (txt == variable)
                {
                    var location = node.GetLocation();
                    int lineNo = location.GetMappedLineSpan().StartLinePosition.Line;

                    if (LineContains(lineNo) == false)
                    {
                        lst.Add(node);
                    }
                }
            }
            else if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.MethodBaseSyntax)
            {
                if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.MethodStatementSyntax)
                {
                    var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.MethodStatementSyntax;
                    string txt = "" + _var.Identifier.ValueText;
                    if (txt == variable)
                    {
                        var location = node.GetLocation();
                        int lineNo = location.GetMappedLineSpan().StartLinePosition.Line;

                        if (LineContains(lineNo) == false)
                        {
                            lst.Add(node);
                        }
                    }
                }
                else
                if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.SubNewStatementSyntax)
                {
                    var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.SubNewStatementSyntax;
                    string txt = "New"; // + _var.SubKeyword.ValueText;
                    if (txt == variable)
                    {
                        var location = node.GetLocation();
                        int lineNo = location.GetMappedLineSpan().StartLinePosition.Line;

                        if (LineContains(lineNo) == false)
                        {
                            lst.Add(node);
                        }
                    }
                }
            }
            else if (node.GetText().ToString() == variable)
            {
                var location = node.GetLocation();
                int lineNo = location.GetMappedLineSpan().StartLinePosition.Line;
                if (LineContains(lineNo) == false)
                {
                    lst.Add(node);
                }
            }

            foreach (var innerNode in node.ChildNodes())
            {
                var location = innerNode.GetLocation();
                int lineNo = location.GetMappedLineSpan().StartLinePosition.Line;
                GetNode(innerNode, variable);
            }
        }

        private bool LineContains(int line)
        {
            bool hasLine = false;

            lst.ForEach(node =>
            {
                var location = node.GetLocation();
                int lineNo = location.GetMappedLineSpan().StartLinePosition.Line;
                hasLine |= lineNo == line;
            });

            return hasLine;
        }

        private void SetUsingLineParent(SyntaxNode node)
        {
            if (node == null) return;

            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.CompilationUnitSyntax)
            {
                return;
            }

            var location = node.GetLocation();
            int lineNo = location.GetMappedLineSpan().StartLinePosition.Line;
            int lineNoEnd = lineNo;

            FindUsingVariableLines[lineNo] = true;
             
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.ClassBlockSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.ClassBlockSyntax;

                if (_var.ClassStatement != null)
                {
                    lineNoEnd = _var.ClassStatement.GetLocation().GetLineSpan().EndLinePosition.Line;
                    for (int loop = lineNoEnd; loop >= lineNo; loop--)
                    {
                        FindUsingVariableLines[loop] = true;
                    }
                }

                if (_var.EndClassStatement != null)
                {
                    lineNoEnd = _var.EndClassStatement.GetLocation().GetLineSpan().StartLinePosition.Line;
                    FindUsingVariableLines[lineNoEnd] = true;
                }
            }
            else
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.VariableDeclaratorSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.VariableDeclaratorSyntax;

                if (_var.Initializer != null)
                {
                    lineNoEnd = _var.Initializer.GetLocation().GetLineSpan().EndLinePosition.Line;
                    for (int loop = lineNoEnd; loop >= lineNo; loop--)
                    {
                        FindUsingVariableLines[loop] = true;
                    }
                }
            }
            else
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.ConstructorBlockSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.ConstructorBlockSyntax;

                if (_var.SubNewStatement != null)
                {
                    lineNoEnd = _var.SubNewStatement.GetLocation().GetLineSpan().EndLinePosition.Line;
                    for (int loop = lineNoEnd; loop >= lineNo; loop--)
                    {
                        FindUsingVariableLines[loop] = true;
                    }
                }
 
                if (_var.BlockStatement != null)
                {
                    lineNoEnd = _var.BlockStatement.GetLocation().GetLineSpan().StartLinePosition.Line - 1;
                    for (int loop = lineNoEnd; loop >= lineNo; loop--)
                    {
                        FindUsingVariableLines[loop] = true;
                    }
                }

                lineNoEnd = _var.EndBlockStatement.GetLocation().GetLineSpan().EndLinePosition.Line;
                FindUsingVariableLines[lineNoEnd] = true;
            }
            else
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.MethodBlockSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.MethodBlockSyntax;

                if (_var.SubOrFunctionStatement != null)
                {
                    lineNoEnd = _var.SubOrFunctionStatement.GetLocation().GetLineSpan().EndLinePosition.Line;
                    for (int loop = lineNoEnd; loop >= lineNo; loop--)
                    {
                        FindUsingVariableLines[loop] = true;
                    }
                }

                if (_var.BlockStatement != null)
                {
                    lineNoEnd = _var.BlockStatement.GetLocation().GetLineSpan().StartLinePosition.Line - 1;
                    for (int loop = lineNoEnd; loop >= lineNo; loop--)
                    {
                        FindUsingVariableLines[loop] = true;
                    }
                }

                lineNoEnd = _var.EndBlockStatement.GetLocation().GetLineSpan().EndLinePosition.Line;
                FindUsingVariableLines[lineNoEnd] = true;
            }
            else
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.ForBlockSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.ForBlockSyntax;

                if (_var.Statements != null && 0 < _var.Statements.Count)
                {
                    lineNoEnd = _var.Statements[0].GetLocation().GetLineSpan().StartLinePosition.Line - 1;
                    for (int loop = lineNoEnd; loop >= lineNo; loop--)
                    {
                        FindUsingVariableLines[loop] = true;
                    }
                }

                if (_var.NextStatement != null)
                {
                    lineNoEnd = _var.NextStatement.GetLocation().GetLineSpan().StartLinePosition.Line;
                    FindUsingVariableLines[lineNoEnd] = true;
                }
            }
            else
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.ForEachBlockSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.ForEachBlockSyntax;

                if (_var.Statements != null && 0 < _var.Statements.Count)
                {
                    lineNoEnd = _var.Statements[0].GetLocation().GetLineSpan().StartLinePosition.Line - 1;
                    for (int loop = lineNoEnd; loop >= lineNo; loop--)
                    {
                        FindUsingVariableLines[loop] = true;
                    }
                }

                if (_var.NextStatement != null)
                {
                    lineNoEnd = _var.NextStatement.GetLocation().GetLineSpan().StartLinePosition.Line;
                    FindUsingVariableLines[lineNoEnd] = true;
                }
            }
            else
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.MultiLineIfBlockSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.MultiLineIfBlockSyntax;

                if (_var.Statements != null && 0 < _var.Statements.Count)  // if ( ~~~ ) 조건절이 여러줄일 경우 범위로 true!
                {
                    lineNoEnd = _var.Statements[0].GetLocation().GetLineSpan().StartLinePosition.Line - 1;
                    for (int loop = lineNoEnd; loop >= lineNo; loop--)
                    {
                        FindUsingVariableLines[loop] = true;
                    }
                }

                if (_var.EndIfStatement != null)
                {
                    lineNoEnd = _var.EndIfStatement.GetLocation().GetLineSpan().StartLinePosition.Line;
                    FindUsingVariableLines[lineNoEnd] = true;
                }
            }
            else
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.TryBlockSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.TryBlockSyntax;

                if (_var.CatchBlocks != null)
                {
                    foreach (var catchblock in _var.CatchBlocks)
                    {
                        int catchlineNo = catchblock.GetLocation().GetLineSpan().StartLinePosition.Line;
                        if (catchblock.Statements != null && 0 < catchblock.Statements.Count)
                        {
                            int catchlineNoEnd = catchblock.Statements[0].GetLocation().GetLineSpan().StartLinePosition.Line - 1;
                            for (int loop = catchlineNoEnd; loop >= catchlineNo; loop--)
                            {
                                FindUsingVariableLines[loop] = true;
                            }
                        }
                    }
                }

                if (_var.FinallyBlock != null)
                {
                    lineNoEnd = _var.FinallyBlock.GetLocation().GetLineSpan().StartLinePosition.Line;
                    FindUsingVariableLines[lineNoEnd] = true;
                }

                if (_var.EndTryStatement != null)
                {
                    lineNoEnd = _var.EndTryStatement.GetLocation().GetLineSpan().StartLinePosition.Line;
                    FindUsingVariableLines[lineNoEnd] = true;
                }
            }
            else
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.UsingBlockSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.UsingBlockSyntax;

                if (_var.EndUsingStatement != null)
                {
                    lineNoEnd = _var.EndUsingStatement.GetLocation().GetLineSpan().StartLinePosition.Line;
                    FindUsingVariableLines[lineNoEnd] = true;
                }
            }
            else
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.SelectBlockSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.SelectBlockSyntax;

                if (_var.EndSelectStatement != null)
                {
                    lineNoEnd = _var.EndSelectStatement.GetLocation().GetLineSpan().StartLinePosition.Line;
                    FindUsingVariableLines[lineNoEnd] = true;
                }
            }
            else
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.WithBlockSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.WithBlockSyntax;

                if (_var.EndWithStatement != null)
                {
                    lineNoEnd = _var.EndWithStatement.GetLocation().GetLineSpan().StartLinePosition.Line;
                    FindUsingVariableLines[lineNoEnd] = true;
                }
            }
            else
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.ReturnStatementSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.ReturnStatementSyntax;

                if (_var.Expression != null)
                {
                    lineNoEnd = _var.Expression.GetLocation().GetLineSpan().EndLinePosition.Line;
                    for (int loop = lineNoEnd; loop >= lineNo; loop--)
                    {
                        FindUsingVariableLines[loop] = true;
                    }
                }
            }
            else
            if (node is Microsoft.CodeAnalysis.VisualBasic.Syntax.InvocationExpressionSyntax)
            {
                var _var = node as Microsoft.CodeAnalysis.VisualBasic.Syntax.InvocationExpressionSyntax;

                if (_var.Expression != null)
                {
                    lineNoEnd = _var.ArgumentList.GetLocation().GetLineSpan().EndLinePosition.Line;
                    for (int loop = lineNoEnd; loop >= lineNo; loop--)
                    {
                        FindUsingVariableLines[loop] = true;
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"X : {lineNo} {node.GetType().FullName}");
            }

            if (node.Parent == null) return;

            SetUsingLineParent(node.Parent);
        } 

        #endregion

        #region C#

        SortedDictionary<int, int> Results = new SortedDictionary<int, int>();

        SortedDictionary<int, string> SourceList { get; set; }

        ViewTextForm viewFm { get; set; }

        private SortedDictionary<int, string> RemoveComment(string str)
        {
            SortedDictionary<int, string> textResultList = new SortedDictionary<int, string>();
            int bugLineIndex = -1;
            try
            {
                string[] lines = str.Replace("\r", "").Split('\n');
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    bugLineIndex = lineIndex;

                    string text = lines[lineIndex];
                    string trimText = text.Trim();

                    if (string.IsNullOrEmpty(trimText)) continue;

                    if (trimText.StartsWith("//")) continue;

                    int commentindex = -1;

                    commentindex = text.IndexOf("//");
                    if (0 < commentindex)
                    {
                        text = text.Substring(0, commentindex);
                    }
                    // text ~ 에서 /* 으로 시작하는 부분에서 ~~~  */ 으로 종료되는 라인까지 skip!처리.
                    bool isMultiCommentBegin = false;
                    int beginMultiCommentPosition = -1;
                    do
                    {
                        if (string.IsNullOrEmpty(text.Trim()))
                        {
                            isMultiCommentBegin = false;
                            break;
                        }

                        if (text.Length <= (beginMultiCommentPosition + 1))
                        {
                            beginMultiCommentPosition = -1;
                        }

                        beginMultiCommentPosition = text.IndexOf("/*", beginMultiCommentPosition + 1);
                        if (0 <= beginMultiCommentPosition)
                        {
                            isMultiCommentBegin = true;
                        }
                        else
                        {
                            isMultiCommentBegin = false; break;
                        }

                        // 라인에 멀티 코멘트가 있으면 종료지점을 찾아.
                        int endMultiCommentPosition = text.IndexOf("*/", beginMultiCommentPosition + 1);
                        if (isMultiCommentBegin && endMultiCommentPosition < 0)
                        {
                            // /* 시작하고 종료가 없는 경우.
                            string strReplace = text.Substring(beginMultiCommentPosition);
                            text = text.Replace(strReplace, "");
                            beginMultiCommentPosition = -1;
                            if (string.IsNullOrEmpty(text.Trim()) == false) continue;

                            if (string.IsNullOrEmpty(text.Trim()) == false) textResultList.Add(lineIndex + 1, text);

                            while (isMultiCommentBegin)
                            {
                                lineIndex++;
                                bugLineIndex = lineIndex;

                                if (lines.Length <= lineIndex)
                                {
                                    isMultiCommentBegin = false;
                                    break;
                                }

                                text = lines[lineIndex];
                                trimText = text.Trim();
                                if (trimText.StartsWith("//")) continue;

                                endMultiCommentPosition = -1;
                                endMultiCommentPosition = text.IndexOf("*/", endMultiCommentPosition + 1);
                                if (endMultiCommentPosition < 0) continue;
                                else
                                {
                                    strReplace = text.Substring(0, endMultiCommentPosition + "*/".Length);
                                    text = text.Replace(strReplace, "");
                                    break;
                                }
                            }
                        }
                        else
                        if (0 <= endMultiCommentPosition && endMultiCommentPosition < text.Length)
                        {
                            // /* ~~ */ 구간이 존재하면.
                            string strReplace = text.Substring(beginMultiCommentPosition, endMultiCommentPosition - beginMultiCommentPosition + "*/".Length);
                            text = text.Replace(strReplace, "");
                            //다시 do ~ 돌면서 /* */를 삭제.
                            continue;
                        }
                    } while (isMultiCommentBegin);

                    if (string.IsNullOrEmpty(text.Trim())) continue;

                    textResultList.Add(lineIndex + 1, text);
                }
            }
            catch (Exception ex)
            {

            }
            return textResultList;
        }

        int currentLine = -1;
        TextSelection CurrentSelection;

        private string GetSelection(bool isAll)
        {
            string setting = "";

            DTE2 _applicationObject = ServiceProvider.GetService(typeof(DTE)) as DTE2;
            //Check active document
            if (_applicationObject.ActiveDocument != null)
            {
                //Get active document
                TextDocument objTextDocument = (TextDocument)_applicationObject.ActiveDocument.Object("");
                if (isAll)
                    objTextDocument.Selection.SelectAll();
                TextSelection objTextSelection = objTextDocument.Selection;

                CurrentSelection = objTextSelection;

                if (!String.IsNullOrEmpty(objTextSelection.Text))
                {
                    if (!isAll)
                        currentLine = objTextSelection.CurrentLine;
                    //Get selected text
                    setting = objTextSelection.Text;
                }
            }
            return setting;
        }

        private void SetSelection(string txt)
        {
            DTE2 _applicationObject = ServiceProvider.GetService(typeof(DTE)) as DTE2;

            //Check active document
            if (_applicationObject.ActiveDocument != null)
            {
                //Get active document
                TextDocument objTextDocument = (TextDocument)_applicationObject.ActiveDocument.Object("");
                TextSelection objTextSelection = objTextDocument.Selection;

                if (!String.IsNullOrEmpty(txt))
                {
                    objTextSelection.Insert(txt, (int)vsInsertFlags.vsInsertFlagsContainNewText);
                    //  objTextDocument.Selection.Text = txt;
                }
            }
        }

        private void ShowViewTextForm(string[] lines)
        {
            //if (viewFm == null) {
            //    viewFm = new ViewTextForm(); 
            //    viewFm.FormClosing += (s, e) =>
            //    {
            //        viewFm = null;
            //        CurrentSelection = null;
            //    }; 
            //} 
            //viewFm.SetTextResult(Results, lines, CurrentSelection);
            //viewFm.Show();
            //viewFm.Activate(); 
        }

        private void ReSearch(string sw, SortedDictionary<int, string> lst)
        {
            List<int> MultiComments = new List<int>();
            List<SearchTemp> bufferStack = new List<SearchTemp>();
            SearchTemp tmp = new SearchTemp();

            SearchTemp Root = null;

            int casecount = 0;

            int limitLineIndex = 0;

            //   string str = string.Join(Environment.NewLine, lst.Values.ToArray()); 
            string[] lines = lst.Values.ToArray();// str.Replace("\r", "").Split('\n');  //.Split(Environment.NewLine.ToArray(), StringSplitOptions.RemoveEmptyEntries);

            bool iscase = false;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string text = lines[lineIndex];

                string trimText = text.Trim();

                // 주석인지 체크
                bool IsCommentLine = CheckCommentLine(lines, lineIndex, limitLineIndex);

                if (IsCommentLine)
                {
                    limitLineIndex = lineIndex;
                    continue;
                }

                int commentindex = -1;
                commentindex = trimText.IndexOf("//");

                if (0 < commentindex)
                {
                    trimText = trimText.Substring(0, commentindex);
                }
                else if (commentindex == 0)
                {
                    limitLineIndex = lineIndex;
                    continue;
                }

                //주석 제거  
                trimText = RemovingComments(trimText);

                if (string.IsNullOrEmpty(trimText.Trim()))
                {
                    limitLineIndex = lineIndex;
                    continue;
                }

                commentindex = trimText.Trim().IndexOf("//");

                if (trimText.StartsWith("case ") || trimText.StartsWith("default ") || trimText.StartsWith("default:"))
                {
                    casecount = 0;
                    if (iscase == false)
                    {
                        SearchTemp newTemp = new SearchTemp();
                        if (Root == null)
                        {
                            Root = newTemp;
                        }
                        tmp.Inner.Add(newTemp);
                        bufferStack.Add(tmp);
                        tmp = newTemp;
                    }
                    tmp.AppendUpper(lineIndex);
                    // todo : 거꾸로 탐색  조건( )
                    LoopContentBack(lines, lineIndex, 0, sw, tmp);
                    casecount++;
                    iscase = true;

                    // case문이 1줄로 작성한 경우..
                    if (0 < casecount && isCaseEnd(trimText))
                    {
                        if (tmp != null) tmp.footer = lineIndex;
                        if (0 <= (bufferStack.Count - 1))
                        {
                            tmp = bufferStack[bufferStack.Count - 1];
                        }
                        bufferStack.Remove(tmp);
                        casecount--;
                        if (casecount < 0) casecount = 0;
                        iscase = false;
                    }
                }
                else if (0 < casecount && (trimText.StartsWith("break") || trimText.StartsWith("return")))
                {
                    //if (tmp != null) tmp.footer = lineIndex;

                    for (int loop = casecount - 1; loop >= 0; loop--)
                    {
                        if (0 <= (bufferStack.Count - 1))
                        {
                            tmp = bufferStack[bufferStack.Count - 1];
                        }
                        bufferStack.Remove(tmp);
                        casecount--;
                    }
                    if (casecount < 0) casecount = 0;
                    iscase = false;
                }

                if (commentindex < 0 && trimText.Contains(sw) && CheckWord(trimText, sw))
                {
                    if (tmp == null)
                    {
                        SearchTemp newTemp = new SearchTemp();
                        if (Root == null)
                        {
                            Root = newTemp;
                        }
                        tmp.Inner.Add(newTemp);
                        bufferStack.Add(tmp);
                        tmp = newTemp;
                    }
                    /*
                    ;
                    {  현재...
                        ;*/
                    //앞뒤로 검색.//  
                    tmp.IsFindUpper = true;
                    tmp.AppendBody(lineIndex);
                    LoopContent(lines, lineIndex, 0, sw, tmp);
                }

                for (int charindex = 0; charindex < trimText.Length; charindex++)
                {
                    if (trimText[charindex] == '{')
                    {
                        SearchTemp newTemp = new SearchTemp();
                        if (Root == null)
                        {
                            Root = newTemp;
                        }
                        tmp.Inner.Add(newTemp);
                        bufferStack.Add(tmp);
                        tmp = newTemp;
                        tmp.AppendUpper(lineIndex);
                        // todo : 거꾸로 탐색  조건( )
                        LoopContentBack(lines, lineIndex, charindex, sw, tmp);
                    }
                    else if (trimText[charindex] == '}')
                    {
                        if (tmp != null) tmp.footer = lineIndex;
                        if (0 <= (bufferStack.Count - 1))
                        {
                            tmp = bufferStack[bufferStack.Count - 1];
                        }
                        bufferStack.Remove(tmp);
                    }
                }
                limitLineIndex = lineIndex;
            }
            Results.Clear();

            Print(Root, lines);
            /* 디버그용.... ShowToolWindow()안에서 처리하는 일!!!
            string xxxxxxxxxxxx = GetDebugString(Root, lines);

            StringBuilder resultString = new StringBuilder();
              
            foreach (KeyValuePair<int, int> key in Results)//.OrderBy(o => o.Key))
            {
                resultString.Append( lines[key.Key].Replace("\t", "    "));
                for (int loop = 0; loop < key.Value; loop++)
                {
                    resultString.AppendLine();
                }
            }
            */
            ShowToolWindow(lines, sw, Language_CSharp);
        }

        private bool isCaseEnd(string trimText)
        {
            bool isEnd = false;
            if (trimText.Replace(" ", "").Trim().EndsWith("break;") || trimText.Replace(" ", "").Trim().EndsWith("return;")) isEnd = true;
            return isEnd;
        }

        private bool CheckCommentLine(string[] lines, int lineIndex, int limitLineIndex)
        {
            // todo : 주석인지 체크!
            // 앞으로 올라가며 
            // /* 를 만나면 주석
            // */ 를 만나면 주석 x
            // limitlineIndex 를 만나면! return

            //*****
            //***** 요렇게 주석넣은거... ㅡ.,ㅡ;;; 

            /*
            //***** ㅁㅁㅁ
            */

            //****
            //***
            //***
            bool isComment = false;

            for (int idx = lineIndex; idx >= limitLineIndex; idx--)
            {
                string txt = lines[idx];

                if (txt.Trim().StartsWith("//"))
                {
                    isComment = false; break;
                }

                char nr = '\0';
                char cr = '\0';

                for (int cIdx = txt.Length - 1; cIdx >= 0; cIdx--)
                {
                    cr = txt[cIdx];

                    if (cr == '/' && nr == '*')
                    {
                        return true;
                    }
                    else if (cr == '*' && nr == '/')
                    {
                        return false;
                    }
                    nr = cr;
                }
            }
            return isComment;
        }

        private string RemovingComments(string trimText)
        {
            string result = "";
            bool iscomment = false;

            char pr = '\0';
            char cr = '\0';
            for (int idx = 0; idx < trimText.Length; idx++)
            {
                cr = trimText[idx];

                if (pr == '*' && cr == '/')
                {
                    if (!iscomment)
                    {
                        // 앞에 모두 주석이므로!! 제거 
                        result = "";
                    }
                    else
                    {
                        iscomment = false;
                    }
                }
                else if (pr == '/' && cr == '*')
                {
                    if (0 < result.Length)
                        result = result.Substring(0, result.Length - 1);
                    iscomment = true;
                }
                else if (pr == '/' && cr == '/')
                {
                    if (0 < result.Length)
                        result = result.Substring(0, result.Length - 1);
                    break;
                }
                else
                {
                    if (iscomment == false) result += cr;
                }
                pr = cr;
            }
            return result;
        }

        private void Print(SearchTemp tmp, string[] lines)
        {
            if (tmp == null) return;

            if (tmp.isFind)
            {
                foreach (var upper in tmp.upper)
                {
                    AppendLine(upper);
                }

                /*
                    if{}
                    else{}

                    if{}
                    else{}
                 
                    이런식으로 있을 경우 해당사항이 없는데도... 표시됨
                    한번더 체크!! 연속인가?를 체크하고 innercount가 있는지 확인!!
                 */
                List<SearchTemp> ifGroup = new List<SearchTemp>();
                string debugString = "";
                for (int loop = 0; loop < tmp.Inner.Count; loop++)
                {
                    /* {} 스코프안에 키워드가 있는지 체크해서 없으면 대상에서 제외!! */
                    var inner = tmp.Inner[loop];
                    if (lines[inner.upper[inner.upper.Count - 1]].Trim().StartsWith("if") && 0 < ifGroup.Count)
                    {
                        bool isFinded = false;
                        foreach (var ifItem in ifGroup)
                        {
                            isFinded |= ifItem.isFind;
                            debugString += GetDebugString(ifItem, lines);
                        }

                        for (int loop2 = ifGroup.Count - 1; !isFinded && loop2 >= 0; loop2--)
                        {
                            tmp.Inner.Remove(ifGroup[loop2]);
                            loop--;
                        }
                        debugString = "";
                        ifGroup.Clear();
                    }
                    ifGroup.Add(inner);
                }

                for (int loop = 0; loop < tmp.Inner.Count; loop++)
                {
                    var inner = tmp.Inner[loop];
                    if (CheckElse(inner, lines) && inner.isFind)
                    {
                        // else 인경우 위에 if문 조건을 보여주기 위해 뿌려줌.
                        for (int rloop = loop - 1; rloop >= 0; rloop--)
                        {
                            var tinner = tmp.Inner[rloop];

                            if (tinner.isFind == false && 0 < tinner.upper.Count)
                            {
                                if (lines[tinner.upper[tinner.upper.Count - 1]].Trim().StartsWith("if") || lines[tinner.upper[tinner.upper.Count - 1]].Trim().StartsWith("else"))
                                {
                                    foreach (var upper in tinner.upper)
                                    {
                                        AppendLine(upper);
                                    }
                                    AppendLine(tinner.footer);
                                }
                                else break;
                            }
                        }
                    }
                    Print(inner, lines);
                }

                foreach (var body in tmp.body)
                {
                    AppendLine(body);
                }

                if (!tmp.upper.Contains(tmp.footer))
                {
                    AppendLine(tmp.footer);
                }

                if (Results.ContainsKey(tmp.footer))
                {
                    Results[tmp.footer] = 1;
                }
            }
        }

        private string GetDebugString(SearchTemp inner, string[] lines)
        {
            string str = "";

            for (int loop = inner.upper.Count - 1; loop >= 0; loop--)
            {
                str += lines[inner.upper[loop]] + Environment.NewLine;
            }

            if (0 < inner.Inner.Count)
            {
                foreach (var item in inner.Inner)
                {
                    str += GetDebugString(item, lines);
                }
            }
            if (!inner.upper.Contains(inner.footer))
            {
                str += lines[inner.footer] + Environment.NewLine;
            }
            return str;
        }

        private bool CheckElse(SearchTemp inner, string[] lines)
        {
            // todo : else 인지 체크하고 검색어가 존재하는지 체크.
            if (0 < inner.upper.Count)
            {

                if (lines[inner.upper[inner.upper.Count - 1]].Trim().StartsWith("else"))
                {
                    return inner.isFind;
                }
            }
            return false;
        }

        private void LoopContent(string[] lines, int lineIndex, int charIndex, string sw, SearchTemp tmp)
        {
            /*
                   ;
                   {  현재...
                       ;*/
            //앞뒤로 검색.// 

            for (int backLineIndex = lineIndex; backLineIndex >= 0; backLineIndex--)
            {
                string btext = lines[backLineIndex];
                string btrimText = btext.Trim();

                int bcommentindex = -1;
                bcommentindex = btrimText.IndexOf("//");

                if (0 < bcommentindex)
                {
                    btrimText = btrimText.Substring(0, bcommentindex);
                }

                if (btrimText.Contains(";") || btrimText.Contains("{") || btrimText.Contains("}"))
                {
                    break;
                }
                tmp.body.Add(backLineIndex);
            }

            for (int frontLineIndex = lineIndex; frontLineIndex < lines.Length; frontLineIndex++)
            {
                string btext = lines[frontLineIndex];
                string btrimText = btext.Trim();

                int bcommentindex = -1;
                bcommentindex = btrimText.IndexOf("//");

                if (0 < bcommentindex)
                {
                    btrimText = btrimText.Substring(0, bcommentindex);
                }


                if (btrimText.Contains(";") || btrimText.Contains("{") || btrimText.Contains("}"))
                {
                    break;
                }
                tmp.body.Add(frontLineIndex);
            }
        }

        private void LoopContentBack(string[] lines, int lineIndex, int charIndex, string sw, SearchTemp tmp)
        {
            // {를 찾은 곳 이전부터 검색! 
            int loopbackIndex = 0;

            int CloseConditionLines = 0;

            if (0 < charIndex)
            {
                // todo : { 가 조건문 바로 뒤에 위치한 경우!!! 
                // 해당 조건에 검색어가 있으면 header에서 찾은것으로 셋팅하기 위해. 
                string btext = lines[lineIndex];
                string btrimText = btext.Trim();

                int bcommentindex = -1;
                bcommentindex = btrimText.IndexOf("//");

                if (0 < bcommentindex)
                {
                    btrimText = btrimText.Substring(0, bcommentindex);
                }

                bcommentindex = btrimText.Trim().IndexOf("//");

                if (bcommentindex == 0) return;

                if (btrimText.ToUpper().StartsWith("CASE"))
                {
                    if (tmp != null) tmp.AppendUpper(lineIndex);
                    return;
                }

                if (btrimText.Contains(sw) && CheckWord(btrimText, sw))
                {
                    if (tmp != null)
                    {
                        tmp.AppendUpper(lineIndex);
                        tmp.IsFindUpper = true;
                    }
                }
            }

            for (int idx = lineIndex - 1; idx >= loopbackIndex; idx--)
            {
                // 거꾸로 체크 > ... 
                string text = lines[idx];
                string trimText = text.Trim();

                int commentindex = -1;
                commentindex = trimText.IndexOf("//");

                if (0 < commentindex)
                {
                    trimText = trimText.Substring(0, commentindex);
                }

                commentindex = trimText.Trim().IndexOf("//");

                if (trimText.ToUpper().StartsWith("#REGION")) break;

                if (trimText.ToUpper().StartsWith("CASE"))
                {
                    if (tmp != null) tmp.AppendUpper(idx);
                    break;
                }

                for (int rcharIndex = trimText.Length - 1; rcharIndex >= 0; rcharIndex--)
                {
                    if (trimText[rcharIndex] == ';')
                    {
                        return;
                    }
                    else if (trimText[rcharIndex] == '}')
                    {
                        return;
                    }
                    else if (trimText[rcharIndex] == '{')
                    {
                        return;
                    }

                    if (text[rcharIndex] == ')')
                    {
                        CloseConditionLines++;
                        if (tmp != null) tmp.AppendUpper(idx);
                    }
                    else if (text[rcharIndex] == '(')
                    {
                        CloseConditionLines--;
                        if (tmp != null) tmp.AppendUpper(idx);
                    }

                    CloseConditionLines++;
                    if (tmp != null) tmp.AppendUpper(idx);

                    if (CloseConditionLines <= 0) break;
                }
                //if (  trimText.ToUpper().StartsWith("PRIVATE") ||
                //    trimText.ToUpper().StartsWith("PROTECTED") ||
                //    trimText.ToUpper().StartsWith("PUBLIC") ||
                //    trimText.ToUpper().StartsWith("#ENDREGION") ||
                //    trimText.ToUpper().StartsWith("NAMESPACE") )
                //    return;

                if (trimText.Contains(sw) && CheckWord(trimText, sw))
                {
                    if (tmp != null)
                    {
                        tmp.AppendUpper(idx);
                        tmp.IsFindUpper = true;
                    }
                    break;
                }

                if (trimText.Contains("namespace") || trimText.Contains("class") || trimText.Contains("else"))
                {
                    if (tmp != null) tmp.AppendUpper(idx);
                    break;
                }

                if (trimText.ToUpper().StartsWith("#ENDREGION"))
                    return;

                if (CloseConditionLines <= 0) break;
            }
        }

        // 변수명으로 인식하는 문자로써 '_'를 포함하였음.
        // 변수_  <-- 처럼 변수로 사용이 가능하므로 체크대상임. 
        string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_";
        string numbers = "0123456789";

        private bool CheckWord(string trimText, string sw)
        {
            //trimText에서 sw 키워드가 단어로 있는지 체크
            // {(숫자|문자)}{sw}(숫자|문자)
            bool isMatch = false;
            int idx = 0;

            do
            {
                int findIdx = trimText.IndexOf(sw, idx);
                if (0 < findIdx)
                {
                    if (!char.IsLetterOrDigit(trimText[findIdx - 1]) &&
                        !alphabet.Contains(trimText[findIdx - 1]) &&
                        !numbers.Contains(trimText[findIdx - 1]))
                    {
                        if ((findIdx + sw.Length) < trimText.Length &&
                            !alphabet.Contains(trimText[findIdx + sw.Length]) &&
                            !numbers.Contains(trimText[findIdx + sw.Length]))
                        {
                            isMatch = true;
                            break;
                        }
                    }
                    idx = findIdx + sw.Length + 1;
                }
                else
                {
                    if (0 == findIdx)
                    {
                        if ((findIdx + sw.Length) < trimText.Length &&
                                !alphabet.Contains(trimText[findIdx + sw.Length]) &&
                                !numbers.Contains(trimText[findIdx + sw.Length]))
                        {
                            isMatch = true;
                            break;
                        }
                    }
                    //-1 못찾으면!
                    break;
                }
            } while (idx < trimText.Length);
            return isMatch;
        }

        private void AppendLine(int lineIndex)
        {
            if (Results.ContainsKey(lineIndex) == false) Results.Add(lineIndex, 1);
        } 
        #endregion

        private IVsWindowFrame m_windowFrame = null;
        ICSharpCode.AvalonEdit.TextEditor editor;

        private void ShowToolWindow(string[] lines, string text, string language)
        {
            // TODO: Change this Guid
            const string TOOLWINDOW_GUID = "EAABD6CD-B3CE-47A9-9E16-922E6DCAB821";

            if (m_windowFrame == null)
            {
                editor = new ICSharpCode.AvalonEdit.TextEditor() { 
                    SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#")
                    ,SelectedText = text 
                };

                editor.Options.EnableRectangularSelection = true;

                editor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.CSharp.CSharpIndentationStrategy(editor.Options);
                editor.ShowLineNumbers = true;

                editor.MouseDoubleClick += Editor_MouseDoubleClick;
                m_windowFrame = CreateToolWindow("변수 사용보기", TOOLWINDOW_GUID, editor);

                // TODO: Initialize m_userControl if required adding a method like:
                //    internal void Initialize(VSPackageToolWindowPackage package)
                // and pass this instance of the package:
                //    m_userControl.Initialize(this);

                //editor.TextArea.SelectionChanged += (sender, args) =>
                //{
                //    if (string.IsNullOrWhiteSpace(editor.SelectedText))
                //    {
                //        foreach (var selectionWordFocus in editor.TextArea.TextView.LineTransformers.OfType<SelectionWordFocusTransformer>().ToList())
                //        {
                //            editor.TextArea.TextView.LineTransformers.Remove(selectionWordFocus);
                //        }
                //    }
                //    else
                //    {
                //        editor.TextArea.TextView.LineTransformers.Add(new SelectionWordFocusTransformer());
                //    }
                //};

                foreach (var selectionWordFocus in editor.TextArea.TextView.LineTransformers.OfType<SelectionWordFocusTransformer>().ToList())
                {
                    editor.TextArea.TextView.LineTransformers.Remove(selectionWordFocus);
                }
                editor.TextArea.TextView.LineTransformers.Add(new SelectionWordFocusTransformer());
            }

            if (editor != null)
            { 
                editor.Clear();

                SelectionWordFocusTransformer.FocusedText = text;

                if (language == Language_Basic)
                {
                    editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("VBNET");

                    foreach (string txt in lines)
                    {
                        editor.Text += txt + Environment.NewLine;
                    }
                }
                else if (language == Language_CSharp)
                {
                    editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");

                    foreach (KeyValuePair<int, int> key in Results)//.OrderBy(o => o.Key))
                    {
                        editor.Text += lines[key.Key].Replace("\t", "    ");
                        for (int loop = 0; loop < key.Value; loop++)
                        {
                            editor.Text += (Environment.NewLine);
                        }
                    }
                }
                SetSelectedTextInEditor(editor, text);
            }
            m_windowFrame.Show();
        }

        private void SetSelectedTextInEditor(ICSharpCode.AvalonEdit.TextEditor editor, string text)
        {
            if (editor == null) return;
            if (string.IsNullOrWhiteSpace(editor.Text)) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            List<string> matchChars = new List<string>() {
                " ", "+", "-", "*", "/", "=", "_", ".", ",", "(", ")", "<", ">",
                "!", "@", "#", "$", "%", "^", "&", "|", "\\", "~", "`", ";", ":",
                "\"", "'", "?", "[", "]", "{", "}",
            };

            for (int loop = 0; loop < editor.Document.LineCount; loop++)
            {
                ICSharpCode.AvalonEdit.Document.DocumentLine dl = editor.Document.Lines[loop];

                string lineText = editor.Document.GetText(dl.Offset, dl.Length);

                int sidx = 0;
                do
                {
                    sidx = lineText.IndexOf(text, sidx, StringComparison.Ordinal); // 대소문자 구분.

                    bool isMatch = true;

                    if (0 < sidx)
                    {
                        isMatch &= matchChars.Contains("" + lineText[sidx - 1]);
                    }

                    if ((sidx + text.Length) < lineText.Length)
                    {
                        isMatch &= matchChars.Contains("" + lineText[sidx + text.Length]);
                    }

                    if (0 < sidx)
                    {
                        editor.Select(dl.Offset + sidx, text.Length);
                        sidx += text.Length + 1;
                    }
                    else
                    {
                        break;
                    }
                }
                while (sidx < lineText.Length);
            }
        }

        private void Editor_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var pos = editor.GetPositionFromPoint(e.GetPosition(editor));

                if (pos.HasValue)
                {
                    string lang = GetLanguage();

                    if (lang == Language_Basic)
                    {
                        int line = Results.ElementAt(pos.Value.Line - 1).Key;

                        if (Results != null)
                        { 
                            if (CurrentSelection != null) CurrentSelection.GotoLine(line + 1, false);
                        }
                    }
                    else if (lang == Language_CSharp)
                    {
                        int line = Results.ElementAt(pos.Value.Line - 1).Key;

                        if (Results != null)
                        {
                            if (SourceList != null)
                            {
                                line = SourceList.Keys.ElementAt(line);
                            }

                            if (CurrentSelection != null) CurrentSelection.GotoLine(line, false);
                        }
                    } 
                }
            }
            catch
            {

            }
        }

        private IVsWindowFrame CreateToolWindow(string caption, string guid, ICSharpCode.AvalonEdit.TextEditor editor)
        {
            const int TOOL_WINDOW_INSTANCE_ID = 0; // Single-instance toolwindow

            IVsUIShell uiShell;
            Guid toolWindowPersistenceGuid;
            Guid guidNull = Guid.Empty;
            int[] position = new int[1];
            int result;
            IVsWindowFrame windowFrame = null;
            uiShell = (IVsUIShell)ServiceProvider.GetService(typeof(SVsUIShell));
            toolWindowPersistenceGuid = new Guid(guid);
            result = uiShell.CreateToolWindow((uint)__VSCREATETOOLWIN.CTW_fInitNew,
                  TOOL_WINDOW_INSTANCE_ID, editor, ref guidNull, ref toolWindowPersistenceGuid,
                  ref guidNull, null, caption, position, out windowFrame);

            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(result);
            return windowFrame;
        }

    }


    public class SearchTemp
    {
        public List<int> upper = new List<int>();

        public List<int> body = new List<int>();

        public int footer { get; set; }

        public List<SearchTemp> Inner = new List<SearchTemp>();

        public bool isFind
        {
            get
            {
                bool __isFind = IsFindUpper;
                __isFind |= GetInnerIsFind(Inner);
                return __isFind;
            }
        }

        private bool GetInnerIsFind(List<SearchTemp> Inner)
        {
            bool __isFinded = false;
            foreach (var innerItem in Inner)
            {
                __isFinded |= innerItem.isFind;
                __isFinded |= GetInnerIsFind(innerItem.Inner);
            }
            return __isFinded;
        }

        public bool IsFindUpper
        {
            get; set;
        }

        internal void AppendUpper(int lineIndex)
        {
            if (upper.Contains(lineIndex) == false) upper.Add(lineIndex);
        }

        internal void RemoveUpper(int lineIndex)
        {
            if (upper.Contains(lineIndex)) upper.Remove(lineIndex);
        }


        internal void AppendBody(int lineIndex)
        {
            if (body.Contains(lineIndex) == false) body.Add(lineIndex);
        }

        internal void RemoveBody(int lineIndex)
        {
            if (body.Contains(lineIndex)) body.Remove(lineIndex);
        }
    }


    [Guid("EAABD6CD-B3CE-47A9-9E16-922E6DCAB821")]
    public class ToolWindow_UsingView : ToolWindowPane
    {
        public ToolWindow_UsingView() : base(null)
        {
            this.Caption = "변수 사용 현황보기";
            this.BitmapResourceID = 301;
            this.BitmapIndex = 1;
        }

        override public IWin32Window Window
        {

            get { return (IWin32Window)Content; }
        }
    } 
}
