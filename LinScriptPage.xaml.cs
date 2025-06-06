using CommunityToolkit.Maui.Alerts;
using Microsoft.Maui.Controls;
using NfcDemo.Lin;

namespace NfcDemo
{
    /// <summary>
    /// A page that allows users to write and execute LIN scripts, displaying output and interacting with NFC tags.
    /// </summary>
    public partial class LinScriptPage : ContentPage
    {
        /// <summary>
        /// The NFC service instance used for discovering tags and sending APDU commands.
        /// </summary>
        readonly INfcService _nfc;

        /// <summary>
        /// Cancellation token source used to cancel ongoing NFC transceive operations.
        /// </summary>
        CancellationTokenSource? _cts;

        /// <summary>
        /// The LIN interpreter extension that executes LIN commands.
        /// </summary>
        LinExtension lin;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinScriptPage"/> class.
        /// Sets up the NFC service, LIN interpreter, and default script content.
        /// </summary>
        public LinScriptPage()
        {
            InitializeComponent();
            _nfc = MauiProgram.Services.GetRequiredService<INfcService>();
            lin = new LinExtension(this, _nfc, AppendLine);

            // Subscribe to NFC tag discovered events.
            _nfc.TagDiscovered += (object? sender, NfcTagEventArgs e) =>
            {
                AppendLine($"Detected card: {BitConverter.ToString(e.Id)}", Colors.GreenYellow);
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                lin.cts = _cts;
            };

            // Prepopulate the script editor with a sample LIN script.
            ScriptEditor.Text = "res = apdu(\"00 A4 04 00 0E 32 50 41 59 2E 53 59 53 2E 44 44 46 30 31 00\")\n";
            ScriptEditor.Text += "status = bytes(res,0,2)\n";
            ScriptEditor.Text += "res = replace(res,status,\"\")\n";

            ScriptEditor.Text += "print(res)\n";
            ScriptEditor.Text += "print()\n";
            ScriptEditor.Text += "tags = tlv(res)\n";
            ScriptEditor.Text += "tree(tags)\n";

            ScriptEditor.Text += "aid = tlv_read(tags,\"6F\",\"A5\",\"BF0C\",\"61\",\"4F\")\n";
            ScriptEditor.Text += "print(\"Aid=\"+aid)\n";

            ScriptEditor.Text += "res = apdu(\"00 A4 04 00 07 \"+aid+\" 00\")\n";
            ScriptEditor.Text += "status = bytes(res,0,2)\n";
            ScriptEditor.Text += "res = replace(res,status,\"\")\n";
            ScriptEditor.Text += "print(res)\n";
            ScriptEditor.Text += "print()\n";
            ScriptEditor.Text += "tags = tlv(res)\n";
            ScriptEditor.Text += "tree(tags)\n";

            ScriptEditor.Text += "sf = tlv_read(tags,\"6F\",\"A5\",\"BF0C\",\"9F4D\")\n";
            ScriptEditor.Text += "sfi = (num(bytes(sf,0),16) << 3) | 0x04\n";
            ScriptEditor.Text += "log_entirs = num(bytes(sf,1),16)\n";
            ScriptEditor.Text += "print(\"SFI = \"+hex(sfi),\" LOGS_COUNT=\"+log_entirs)\n";

            ScriptEditor.Text += "for(i=1;i<=log_entirs;i = i + 1){\n";
            ScriptEditor.Text += "\t res = apdu(\"00 B2 \"+hex(i,\"2\")+\" \"+hex(sfi,\"2\")+\" 00\")\n";
            ScriptEditor.Text += "\t status = bytes(res,0,2)\n";
            ScriptEditor.Text += "\t if(bytes(res,0) != \"90\"){\n";
            ScriptEditor.Text += "\t error(\"Missing rest of log files\")\n";
            ScriptEditor.Text += "}\n";
            ScriptEditor.Text += "res = replace(res,status,\"\")\n";
            ScriptEditor.Text += "\t value1 = bytes(res,1,5)\n";
            ScriptEditor.Text += "\t value2 = bytes(res,6)\n";
            ScriptEditor.Text += "\t date = bytes(res,9,3)\n";
            ScriptEditor.Text += "\t print(res)\n";
            ScriptEditor.Text += "\t print(\"Day: \"+bytes(date,2)+\".\"+bytes(date,1)+\".20\"+bytes(date,0)+\"  Value = \"+hex(num(value1,16))+\".\"+value2+\"zl\")\n";
            ScriptEditor.Text += "}\n";
        }

        /// <summary>
        /// Called when the page appears. Resumes NFC tag discovery.
        /// </summary>
        protected override void OnAppearing()
        {
            base.OnAppearing();
            _nfc.Resume();
        }

        /// <summary>
        /// Called when the page is no longer visible. Pauses NFC tag discovery.
        /// </summary>
        protected override void OnDisappearing()
        {
            _nfc.Pause();
            base.OnDisappearing();
        }

        /// <summary>
        /// Appends a line of text with an optional color to the output label and scrolls to the bottom.
        /// </summary>
        /// <param name="text">The text to append.</param>
        /// <param name="color">The color of the text. Defaults to white if <c>null</c>.</param>
        public void AppendLine(string text, Color? color = null)
        {
            color ??= Colors.White;

            var fs = OutputLabel.FormattedText ??= new FormattedString();
            var span = new Span
            {
                Text = text + Environment.NewLine,
                TextColor = color
            };

            var tap = new TapGestureRecognizer
            {
                NumberOfTapsRequired = 1
            };
            tap.Tapped += (o, t) =>
            {
                Toast.Make("Message copied");
                Clipboard.SetTextAsync(text);
            };

            fs.Spans.Add(span);

            LastLogLabel.Text = text;
            LastLogLabel.TextColor = (Color)color;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await OutputScroll.ScrollToAsync(0, OutputLabel.Height, animated: true);
            });
        }

        /// <summary>
        /// Selects and highlights the specified line number in the given <see cref="Editor"/>.
        /// </summary>
        /// <param name="editor">The editor control whose text will be updated.</param>
        /// <param name="lineNumber">The 1-based line number to select and highlight.</param>
        public static void SelectLine(Editor editor, int lineNumber)
        {
            if (lineNumber < 1 || editor == null)
                return;

            string text = editor.Text ?? string.Empty;

            // 1. find index of first character
            int start = 0;          // start of the current line
            int current = 1;        // current line number

            for (int i = 0; i < text.Length && current < lineNumber; i++)
            {
                if (text[i] == '\n')
                {
                    current++;
                    start = i + 1;  // move past '\n'
                }
            }

            if (current != lineNumber) // line out of range
                return;

            int end = text.IndexOf('\n', start);
            if (end == -1) end = text.Length;

            editor.CursorPosition = start;
            editor.SelectionLength = end - start;
            editor.Focus();
        }

        /// <summary>
        /// Handles the Run button click event. Reinitializes the LIN interpreter and executes each script line sequentially.
        /// </summary>
        /// <param name="sender">The button that was clicked.</param>
        /// <param name="e">Event arguments.</param>
        private void OnRunClicked(object sender, EventArgs e)
        {
            lin = new LinExtension(this, _nfc, AppendLine);
            lin.cts = _cts;

            AppendLine("Running script…", Colors.CornflowerBlue);
            var code = ScriptEditor.Text.Split('\n');

            for (int i = 0; i < code.Length; i++)
            {
                try
                {
                    var result = lin.Execute(code[i] + "\n");
                    if (result is not null)
                        AppendLine(result.ToString(), Colors.ForestGreen);
                }
                catch (Exception ex)
                {
                    AppendLine($"[Line {i + 1}]Error: {ex.Message}", Colors.Red);
                    SelectLine(ScriptEditor, i + 1);
                    break;
                }
            }
        }
    }
}
