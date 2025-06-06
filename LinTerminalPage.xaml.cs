namespace NfcDemo;
using Libs.TLV;
using Microsoft.Maui.Controls.Shapes;
using NfcDemo.Lin;

public partial class LinTerminalPage : ContentPage
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
    /// The LIN interpreter extension that executes LIN commands in this terminal.
    /// </summary>
    LinExtension lin;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinTerminalPage"/> class.
    /// Subscribes to NFC tag discovery events and sets up the output scrolling behavior.
    /// </summary>
    public LinTerminalPage()
    {
        InitializeComponent();
        OutputStack.SizeChanged += (o, p) => OutputScroll.ScrollToAsync(OutputStack,
                                            ScrollToPosition.End,
                                            animated: true);
        _nfc = MauiProgram.Services.GetRequiredService<INfcService>();
        lin = new LinExtension(this, _nfc, AppendMessage);
        _nfc.TagDiscovered += (object? sender, NfcTagEventArgs e) =>
        {
            AppendMessage($"Detected card: {BitConverter.ToString(e.Id)}", Colors.GreenYellow);
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            lin.cts = _cts;
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
    /// Handles the Send button click event. Executes the entered LIN command and displays the result.
    /// </summary>
    /// <param name="sender">The button that was clicked.</param>
    /// <param name="e">Event arguments.</param>
    private async void OnSendClicked(object sender, EventArgs e)
    {
        AppendCommand(Input.Text);

        try
        {
            var result = lin.Execute(Input.Text);
            AppendCommand(result is not null ? result.ToString() : "(ok)", Colors.ForestGreen);
        }
        catch (Exception ex)
        {
            AppendMessage($"Error: {ex.Message}", Colors.Red);
        }

        Input.Text = "";
    }

    /// <summary>
    /// Handles the tap event on a line in the output or command history.
    /// Executes the associated <see cref="MessageOptions"/> action.
    /// </summary>
    /// <param name="sender">The tapped label.</param>
    /// <param name="e">Event arguments.</param>
    private async void OnLineTapped(object sender, EventArgs e)
    {
        if (sender is Label lbl && lbl.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer rec)
            (rec.CommandParameter as MessageOptions)?.Invoke(this);
    }

    /// <summary>
    /// Appends a message line to the terminal output with an optional color and context menu actions.
    /// </summary>
    /// <param name="text">The message text to display.</param>
    /// <param name="color">The text color. Defaults to black if <c>null</c>.</param>
    void AppendMessage(string text, Color? color = null)
    {
        var label = new Label
        {
            Text = text,
            FontFamily = "Consolas",
            FontSize = 14,
            TextColor = color ?? Colors.Black,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var tap = new TapGestureRecognizer();
        tap.NumberOfTapsRequired = 1;
        tap.CommandParameter = new MessageOptions(text, text,
            ("Copy", (ctx) => Clipboard.Default.SetTextAsync((string)ctx)),
            ("Clear Terminal", (ctx) => OutputStack.Children.Clear())
        );
        tap.Tapped += OnLineTapped;
        label.GestureRecognizers.Add(tap);

        OutputStack.Children.Add(label);
    }

    /// <summary>
    /// Appends a command line to the terminal output with an optional color and context menu actions.
    /// </summary>
    /// <param name="text">The command text to display.</param>
    /// <param name="color">The text color. Defaults to black if <c>null</c>.</param>
    void AppendCommand(string text, Color? color = null)
    {
        var label = new Label
        {
            Text = text,
            FontFamily = "Consolas",
            FontSize = 14,
            TextColor = color ?? Colors.Black,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var tap = new TapGestureRecognizer();
        tap.NumberOfTapsRequired = 1;
        tap.CommandParameter = new MessageOptions(text, text,
            ("Copy", (ctx) => Clipboard.Default.SetTextAsync((string)ctx)),
            ("Repeat command", (ctx) => { Input.Text = (string)ctx; OnSendClicked(null, null); }
        ),
            ("Clear Terminal", (ctx) => OutputStack.Children.Clear())
        );
        tap.Tapped += OnLineTapped;
        label.GestureRecognizers.Add(tap);

        OutputStack.Children.Add(label);
    }
}
