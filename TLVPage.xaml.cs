using Libs.TLV;

namespace NfcDemo
{
    /// <summary>
    /// Page that parses a TLV-encoded hex string, displays its tree representation, 
    /// and allows copying the tree or JSON representation to the clipboard.
    /// </summary>
    public partial class TLVPage : ContentPage
    {
        /// <summary>
        /// Holds the most recently generated tree string representation of the TLV data.
        /// </summary>
        private string lastTree = "";

        /// <summary>
        /// Holds the most recently generated JSON representation of the TLV data.
        /// </summary>
        private string lastTreeJSON = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="TLVPage"/> class with a default TLV input.
        /// </summary>
        public TLVPage()
        {
            InitializeComponent();
            InputTLV.Text = "6F 38 84 07 A0 00 00 00 04 10 10 A5 2D 50 0A 4D 61 73 74 65\r\n" +
                            "72 63 61 72 64 87 01 01 5F 2D 02 65 6E BF 0C 16 9F 4D 02 0B\r\n" +
                            "0A 9F 6E 07 06 16 00 00 30 30 00 9F 0A 04 00 01 01 02\r\n";
            TryConvert();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TLVPage"/> class with a provided TLV input string.
        /// </summary>
        /// <param name="inputText">The TLV-encoded hex string to parse and display.</param>
        public TLVPage(string inputText)
        {
            InitializeComponent();
            InputTLV.Text = inputText;
            TryConvert();
        }

        /// <summary>
        /// Event handler for when the TLV input text changes. Currently no-op.
        /// </summary>
        /// <param name="sender">The input control.</param>
        /// <param name="e">Event data for text changes.</param>
        private void InputTLV_TextChanged(object sender, TextChangedEventArgs e)
        {
            // No additional behavior on text change; conversion is triggered manually.
        }

        /// <summary>
        /// Attempts to parse the text in <see cref="InputTLV"/> as TLV-encoded data,
        /// generates both a tree and JSON representation, and updates the UI.
        /// </summary>
        private void TryConvert()
        {
            lastTree = "";
            lastTreeJSON = "";

            try
            {
                // Parse the hex string into a TLV structure.
                var tlv = TlvParser.Parse(TlvParser.HexStringToBytes(InputTLV.Text));

                // Generate and display the tree representation.
                OutpuTLV.Text = TreePrinter.GenerateTreeString(tlv);
                OutpuTLV.TextColor = Colors.White;
                lastTree = OutpuTLV.Text;

                // Generate and store the JSON representation.
                lastTreeJSON = TlvParser.ToJson(tlv);
            }
            catch (Exception error)
            {
                // Display any parsing errors in red.
                OutpuTLV.Text = error.Message;
                OutpuTLV.TextColor = Colors.Red;
            }
        }

        /// <summary>
        /// Handles the Convert button click event. Retriggers the TLV conversion process.
        /// </summary>
        /// <param name="sender">The Convert button.</param>
        /// <param name="e">Event data for the click event.</param>
        private void ConvertBtn_Clicked(object sender, EventArgs e)
        {
            TryConvert();
        }

        /// <summary>
        /// Handles the Copy button click event. Copies the most recent tree representation to the clipboard.
        /// </summary>
        /// <param name="sender">The Copy button.</param>
        /// <param name="e">Event data for the click event.</param>
        private void CopyBtn_Clicked(object sender, EventArgs e)
            => Clipboard.Default.SetTextAsync(lastTree);

        /// <summary>
        /// Handles the Copy JSON button click event. Copies the most recent JSON representation to the clipboard.
        /// </summary>
        /// <param name="sender">The Copy JSON button.</param>
        /// <param name="e">Event data for the click event.</param>
        private void CopyJSONBtn_Clicked(object sender, EventArgs e)
            => Clipboard.Default.SetTextAsync(lastTreeJSON);
    }
}
