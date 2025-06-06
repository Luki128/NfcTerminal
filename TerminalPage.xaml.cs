using static System.Runtime.InteropServices.JavaScript.JSType;

namespace NfcDemo;
using Libs.TLV;

/// <summary>
/// Represents a set of context-specific options presented to the user via an action sheet.
/// </summary>
public class MessageOptions
{
    private object context;
    private (string option, Action<object> callback)[] options;
    private string msg = "";

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageOptions"/> class with the specified context, message, and options.
    /// </summary>
    /// <param name="context">An arbitrary object passed to each option's callback when invoked.</param>
    /// <param name="msg">The base message text displayed or truncated in the action sheet title.</param>
    /// <param name="options">
    /// A list of tuples, each containing an option label and a callback action taking the <paramref name="context"/> as a parameter.
    /// </param>
    public MessageOptions(object context, string msg, params (string option, Action<object> callback)[] options)
    {
        this.context = context;
        this.options = options;
        this.msg = msg;
    }

    /// <summary>
    /// Truncates the input string to a maximum length, appending ellipsis if it exceeds that length.
    /// </summary>
    /// <param name="str">The string to possibly truncate.</param>
    /// <param name="maxLng">The maximum allowed length of the string before truncation.</param>
    /// <returns>
    /// The original string if its length is less than or equal to <paramref name="maxLng"/>;
    /// otherwise, a truncated string of length <paramref name="maxLng"/> followed by "…".
    /// </returns>
    private string CutString(string str, int maxLng)
    {
        if (str.Length > maxLng)
            return msg.Substring(0, maxLng) + "...";
        else
            return msg;
    }

    /// <summary>
    /// Displays an action sheet on the specified <see cref="Page"/> with the defined options.
    /// When the user selects an option, invokes the corresponding callback with the provided context.
    /// </summary>
    /// <param name="page">The <see cref="Page"/> on which to display the action sheet.</param>
    public async void Invoke(Page page)
    {
        string action = await page.DisplayActionSheet(
            $"Options to: {CutString(msg, 10)}",
            "Cancel",
            null,
            options.Select(x => x.option).ToArray()
        );

        options.FirstOrDefault(x => x.option == action).callback?.Invoke(context);
    }
}

public partial class TerminalPage : ContentPage
{
    /// <summary>
    /// The NFC service instance used for discovering tags and performing transceive operations.
    /// </summary>
    readonly INfcService _nfc;

    /// <summary>
    /// Cancellation token source used to cancel ongoing NFC transceive calls.
    /// </summary>
    CancellationTokenSource? _cts;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalPage"/> class.
    /// Subscribes to NFC tag discovery events and sets up automatic scrolling of the output stack.
    /// </summary>
    public TerminalPage()
    {
        InitializeComponent();

        // Scroll to end whenever the output stack's size changes.
        OutputStack.SizeChanged += (o, p) =>
            OutputScroll.ScrollToAsync(OutputStack, ScrollToPosition.End, animated: true);

        _nfc = MauiProgram.Services.GetRequiredService<INfcService>();
        _nfc.TagDiscovered += (object? sender, NfcTagEventArgs e) =>
        {
            AppendMessage($"Detected card: {BitConverter.ToString(e.Id)}", Colors.GreenYellow);
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
        };
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
    /// Handles the Send button click event. Sends the hex string from the input field as an APDU command to the NFC tag,
    /// then displays the response or any error encountered.
    /// </summary>
    /// <param name="sender">The button that was clicked.</param>
    /// <param name="e">Event arguments.</param>
    private async void OnSendClicked(object sender, EventArgs e)
    {
        AppendCommand(Input.Text);

        try
        {
            // Convert the input hex string to bytes and send via NFC.
            var resp = await _nfc.TransceiveAsync(TlvParser.HexStringToBytes(Input.Text), _cts.Token);
            var (data, sw) = Apdu.SplitResponse(resp);

            AppendAPDU(data, sw, Colors.CornflowerBlue);
        }
        catch (Exception ex) when (ex is TaskCanceledException or InvalidOperationException)
        {
            // Display cancellation or invalid operation errors.
            AppendMessage(ex.Message, Colors.Red);
        }

        Input.Text = "";
    }

    /// <summary>
    /// Handles the tap event on any output line. If the tapped label has an associated <see cref="MessageOptions"/>,
    /// invokes the corresponding action.
    /// </summary>
    /// <param name="sender">The tapped <see cref="Label"/>.</param>
    /// <param name="e">Event arguments.</param>
    private async void OnLineTapped(object sender, EventArgs e)
    {
        if (sender is Label lbl && lbl.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer rec)
            (rec.CommandParameter as MessageOptions)?.Invoke(this);
    }

    /// <summary>
    /// Appends a generic message line to the terminal output with an optional color and context menu actions.
    /// </summary>
    /// <param name="text">The message text to display.</param>
    /// <param name="color">The color of the text. Defaults to black if <c>null</c>.</param>
    void AppendMessage(string text, Color? color = null)
    {
        var label = new Label
        {
            Text = text,
            FontFamily = "Consolas",
            FontSize = 14,
            TextColor = color ?? Colors.Black,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var tap = new TapGestureRecognizer
        {
            NumberOfTapsRequired = 1,
            CommandParameter = new MessageOptions(
                text,
                text,
                ("Copy", (ctx) => Clipboard.Default.SetTextAsync((string)ctx)),
                ("Clear Terminal", (ctx) => OutputStack.Children.Clear())
            )
        };
        tap.Tapped += OnLineTapped;
        label.GestureRecognizers.Add(tap);

        OutputStack.Children.Add(label);
    }

    /// <summary>
    /// Appends a command line (the user’s input) to the terminal output with an optional color and context menu actions.
    /// </summary>
    /// <param name="text">The command text to display.</param>
    /// <param name="color">The color of the text. Defaults to black if <c>null</c>.</param>
    void AppendCommand(string text, Color? color = null)
    {
        var label = new Label
        {
            Text = text,
            FontFamily = "Consolas",
            FontSize = 14,
            TextColor = color ?? Colors.Black,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var tap = new TapGestureRecognizer
        {
            NumberOfTapsRequired = 1,
            CommandParameter = new MessageOptions(
                text,
                text,
                ("Copy", (ctx) => Clipboard.Default.SetTextAsync((string)ctx)),
                ("Repeat command", (ctx) => { Input.Text = (string)ctx; OnSendClicked(null, null); }
            ),
                ("Clear Terminal", (ctx) => OutputStack.Children.Clear())
            )
        };
        tap.Tapped += OnLineTapped;
        label.GestureRecognizers.Add(tap);

        OutputStack.Children.Add(label);
    }

    /// <summary>
    /// Appends an APDU response to the terminal output, showing status word and data bytes,
    /// with context menu actions to copy SW, copy data, convert data to TLV, or clear the terminal.
    /// </summary>
    /// <param name="data">The data bytes returned from the APDU response.</param>
    /// <param name="sw">The status word as a <c>ushort</c>.</param>
    /// <param name="color">The color of the text. Defaults to black if <c>null</c>.</param>
    void AppendAPDU(byte[] data, ushort sw, Color? color = null)
    {
        var label = new Label
        {
            Text = $"SW={sw:X4}\nData={BitConverter.ToString(data)}",
            FontFamily = "Consolas",
            FontSize = 14,
            TextColor = color ?? Colors.Black,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var tap = new TapGestureRecognizer
        {
            NumberOfTapsRequired = 1,
            CommandParameter = new MessageOptions(
                new Tuple<string, string, string>(label.Text, BitConverter.ToString(data), $"{sw:X4}"),
                label.Text,
                ("Copy", (ctx) => Clipboard.Default.SetTextAsync(((Tuple<string, string, string>)ctx).Item1)),
                ("Copy SW", (ctx) => Clipboard.Default.SetTextAsync(((Tuple<string, string, string>)ctx).Item3)),
                ("Copy Data", (ctx) => Clipboard.Default.SetTextAsync(((Tuple<string, string, string>)ctx).Item2)),
                ("Try convert to TLV", (ctx) => Navigation.PushAsync(new TLVPage(((Tuple<string, string, string>)ctx).Item2))),
                ("Clear Terminal", (ctx) => OutputStack.Children.Clear())
            )
        };
        tap.Tapped += OnLineTapped;
        label.GestureRecognizers.Add(tap);

        OutputStack.Children.Add(label);
    }
}
