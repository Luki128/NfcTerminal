#if ANDROID
using Android.App;
using Android.Nfc;
using Android.Nfc.Tech;
using Android.OS;
using Java.Lang;
using Microsoft.Maui.ApplicationModel;

namespace NfcDemo.Platforms.Android
{
    /// <summary>
    /// Android implementation of the <see cref="INfcService"/> interface using NFC reader mode.
    /// </summary>
    public class NfcAndroidService : Java.Lang.Object, NfcAdapter.IReaderCallback, INfcService
    {
        /// <summary>
        /// The NFC adapter for the current device.
        /// </summary>
        NfcAdapter? _adapter;

        /// <summary>
        /// The current Android activity context.
        /// </summary>
        Activity? _activity;

        /// <summary>
        /// Raised when an NFC tag is discovered.
        /// </summary>
        public event EventHandler<NfcTagEventArgs>? TagDiscovered;

        /// <summary>
        /// Synchronization object for accessing the <see cref="_iso"/> field.
        /// </summary>
        readonly object _sync = new();

        /// <summary>
        /// The connected <see cref="IsoDep"/> instance representing the ISO-DEP tag.
        /// </summary>
        IsoDep? _iso;

        /// <summary>
        /// Enables NFC reader mode and begins listening for tags.
        /// </summary>
        public void Resume()
        {
            _activity = (Activity?)Platform.CurrentActivity;
            _adapter ??= NfcAdapter.GetDefaultAdapter(_activity);
            if (_adapter == null) return;  // missing NFC

            var flags = NfcReaderFlags.NfcA |
                        NfcReaderFlags.NfcB |
                        NfcReaderFlags.NfcF |
                        NfcReaderFlags.NfcV |
                        //NfcReaderFlags.IsoDep |
                        NfcReaderFlags.SkipNdefCheck;

            _adapter.EnableReaderMode(_activity, this, flags, Bundle.Empty);
        }

        /// <summary>
        /// Disables NFC reader mode and stops listening for tags.
        /// </summary>
        public void Pause() =>
            _adapter?.DisableReaderMode(_activity);

        /// <summary>
        /// Sends an APDU command to the connected NFC tag and receives the response.
        /// </summary>
        /// <param name="command">The APDU command bytes to send.</param>
        /// <param name="ct">A cancellation token to cancel the operation.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the response bytes from the tag.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if no tag is connected.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the tag has been removed during the operation.</exception>
        /// <exception cref="InvalidOperationException">Thrown if an I/O error occurs while communicating with the NFC tag.</exception>
        public async Task<byte[]> TransceiveAsync(byte[] command, CancellationToken ct = default)
        {
            IsoDep? iso;
            lock (_sync) iso = _iso;
            if (iso == null || !iso.IsConnected)
                throw new InvalidOperationException("No tag connected");

            try
            {
                return await Task.Run(() => iso.Transceive(command), ct);
            }
            catch (TagLostException)
            {
                throw new InvalidOperationException("The tag has been removed");
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException("I/O Error NFC", ex);
            }
        }

        /// <summary>
        /// Callback invoked by the NFC adapter when a tag is discovered.
        /// </summary>
        /// <param name="tag">The discovered NFC tag.</param>
        public void OnTagDiscovered(Tag tag)
        {
            var iso = IsoDep.Get(tag);
            if (iso == null) return;  

            try
            {
                iso.Connect();
                iso.Timeout = 5000;    

                lock (_sync) _iso = iso;           
                var id = tag.GetId();           
                MainThread.BeginInvokeOnMainThread(() =>
                    TagDiscovered?.Invoke(this, new(id)));
            }
            catch
            {
                iso.Close();
            }
        }
    }
}
#endif
