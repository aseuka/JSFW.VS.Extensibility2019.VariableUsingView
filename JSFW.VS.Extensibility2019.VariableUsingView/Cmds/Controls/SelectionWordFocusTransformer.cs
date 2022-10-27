using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JSFW.VS.Extensibility.Cmds.Controls
{
    public class SelectionWordFocusTransformer : ICSharpCode.AvalonEdit.Rendering.DocumentColorizingTransformer
    {
        public static string FocusedText { get; set; } = "";

        protected override void ColorizeLine(ICSharpCode.AvalonEdit.Document.DocumentLine line)
        {
            if (string.IsNullOrWhiteSpace(SelectionWordFocusTransformer.FocusedText)) return; 
            
            string text = CurrentContext.Document.GetText(line.Offset, line.Length);

            int sidx = 0;
            List<string> matchChars = new List<string>() {
                " ", "+", "-", "*", "/", "=", "_", ".", ",", "(", ")", "<", ">", 
                "!", "@", "#", "$", "%", "^", "&", "|", "\\", "~", "`", ";", ":",
                "\"", "'", "?", "[", "]", "{", "}",
            };
            do
            {
                sidx = text.IndexOf(SelectionWordFocusTransformer.FocusedText, sidx, StringComparison.Ordinal); // 대소문자 구분.
                if (0 < sidx)
                {
                    //문자열로만 체크하니 포함된 문자열도 검색이 되어...
                    //FN을 검색했을때 FNS 요런것도 검색이 됨. 
                    //앞, 뒤를 모두 검색해봐야 함. 
                    bool isMatch = true;

                    if (0 < sidx)
                    {
                        isMatch &= matchChars.Contains("" + text[sidx - 1]);
                    }

                    if ((sidx+ SelectionWordFocusTransformer.FocusedText.Length) < text.Length)
                    {
                        isMatch &= matchChars.Contains("" + text[sidx + SelectionWordFocusTransformer.FocusedText.Length]);
                    }

                    if (isMatch)
                    {
                        base.ChangeLinePart(line.Offset + sidx,
                            line.Offset + sidx + SelectionWordFocusTransformer.FocusedText.Length,
                            (ICSharpCode.AvalonEdit.Rendering.VisualLineElement element) =>
                            {
                                // This lambda gets called once for every VisualLineElement
                                // between the specified offsets.
                                System.Windows.Media.Typeface tf = element.TextRunProperties.Typeface;
                                // Replace the typeface with a modified version of
                                // the same typeface
                                element.TextRunProperties.SetTypeface(new System.Windows.Media.Typeface(
                                        tf.FontFamily,
                                        System.Windows.FontStyles.Oblique,
                                        System.Windows.FontWeights.Bold, 
                                        tf.Stretch
                                    ));
                                var underLineDecorations = new System.Windows.TextDecorationCollection();
                                underLineDecorations.Add(
                                    new System.Windows.TextDecoration(
                                            System.Windows.TextDecorationLocation.Underline,
                                            new System.Windows.Media.Pen(System.Windows.Media.Brushes.Red, 2f),
                                            0d,
                                            System.Windows.TextDecorationUnit.FontRecommended,
                                            System.Windows.TextDecorationUnit.FontRecommended));
                                element.TextRunProperties.SetTextDecorations(underLineDecorations);

                                element.TextRunProperties.SetForegroundBrush(System.Windows.Media.Brushes.Red);
                            });
                    }
                    sidx += SelectionWordFocusTransformer.FocusedText.Length + 1;
                }
                else
                {
                    break;
                }
            }
            while (sidx < text.Length);
        }
    }
}
