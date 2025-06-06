//using Android.Nfc;
//using Android.OS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

/// <summary>
/// Provides data for NFC tag discovery events.
/// </summary>
public class NfcTagEventArgs : EventArgs
{
    /// <summary>
    /// Gets the unique identifier (UID) of the discovered NFC tag.
    /// </summary>
    public byte[] Id { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NfcTagEventArgs"/> class with the specified tag ID.
    /// </summary>
    /// <param name="id">The byte array representing the NFC tag's UID.</param>
    public NfcTagEventArgs(byte[] id)
    {
        Id = id;
    }
}

/// <summary>
/// Defines the contract for an NFC service capable of discovering tags and sending APDU commands.
/// </summary>
public interface INfcService
{
    /// <summary>
    /// Occurs when an NFC tag is discovered.
    /// </summary>
    event EventHandler<NfcTagEventArgs> TagDiscovered;

    /// <summary>
    /// Sends an APDU command to the connected NFC tag and returns the response.
    /// </summary>
    /// <param name="command">The APDU command bytes to send.</param>
    /// <param name="ct">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation.
    /// The task result contains the response bytes from the tag.
    /// </returns>
    Task<byte[]> TransceiveAsync(byte[] command, CancellationToken ct = default);

    /// <summary>
    /// Enables or resumes NFC tag discovery.
    /// </summary>
    void Resume();

    /// <summary>
    /// Pauses or disables NFC tag discovery.
    /// </summary>
    void Pause();
}

/// <summary>
/// A dummy implementation of <see cref="INfcService"/> for testing or mock purpose.
/// </summary>
public class DummyNfcService : INfcService
{
    /// <summary>
    /// Occurs when an NFC tag is discovered. This event is never raised by the dummy implementation.
    /// </summary>
    public event EventHandler<NfcTagEventArgs> TagDiscovered;

    /// <summary>
    /// Starts or resumes NFC tag discovery. The dummy implementation does nothing.
    /// </summary>
    public void Resume()
    {
    }

    /// <summary>
    /// Pauses or disables NFC tag discovery. The dummy implementation does nothing.
    /// </summary>
    public void Pause()
    {
    }

    /// <summary>
    /// Attempts to send an APDU command to the connected NFC tag. The dummy implementation returns null.
    /// </summary>
    /// <param name="command">The APDU command bytes to send.</param>
    /// <param name="ct">A cancellation token. This parameter is ignored by the dummy implementation.</param>
    /// <returns>
    /// Always returns <c>null</c> in the dummy implementation.
    /// </returns>
    public Task<byte[]> TransceiveAsync(byte[] command, CancellationToken ct = default)
    {
        return null;
    }
}
