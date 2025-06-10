# NFC Demo – Android App ([Doc page](https://luki128.github.io/NfcTerminal/lin_extension.html))

**NFC Demo** is an Android-only application for experimenting with NFC smart cards. It provides four main “pages” (screens), each tailored to a specific aspect of NFC development and testing:

1. **Raw APDU Terminal**  
   Send and receive low-level APDU commands directly to/from an NFC card.
2. **Lin Interactive Terminal**  
   A REPL-style interface powered by the custom **Lin** scripting language, letting you send APDU commands, parse responses, and incorporate logic on the fly.  
   > For more information on Lin, see the [Lin-language GitHub repo](https://luki128.github.io/NfcTerminal/index.html).
3. **Lin Script Builder**  
   A full‐featured script editor where you can compose, and run multi‐line Lin scripts that interact with NFC cards.
4. **TLV Parser / Tree Viewer**  
   Feed a hex-encoded TLV response into a parser and view it as a nested, ASCII-art tree. (You can also pipe APDU responses from the Raw Terminal into this page.)

---

## Key Facts

- **Platform:** Android only (no iOS or desktop support at present).  
- **Architecture:** Built with .NET MAUI for Android.  
- **Pages / Screens:**  
  1. **NFC Terminal (Raw APDU)**  
     - Text‐based terminal (90% of the screen) for typing APDU hex commands.  
     - A “Send” button to push the selected APDU to the card.  
     - Scrollable output area showing card responses (status words + data).  
  2. **Lin Interactive Terminal**  
     - REPL prompt for Lin language commands.  
     - Built-in Lin functions include `print`, `apdu(...)`, `tlv(...)`, `tree(...)`, etc.  
     - On each `apdu(...)` call, Lin will send the hex command to the card and display the response.  
     - Colors, error reporting, and simple script fragments can be tested here.  
  3. **Lin Script Builder**  
     - Simple multiline code editor.  
     - “Run” button at the top.  
     - Output area at the bottom, similar to the interactive terminal.    
  4. **TLV Parser / Tree Viewer**  
     - Paste or pipe a hex string (e.g. an APDU response) into the input area.  
     - Instantly parses Tag-Length-Value structures.  
     - Displays a nested tree in ASCII-art format, showing tags, lengths, values, and hierarchy. 
---

## Getting Started

1. **Install & Run (Android only)**  
   - Clone or download this repository.  
   - Open in Visual Studio 2022/2023 with .NET MAUI workload installed.  
   - Ensure your Android SDK minimum target is set to API 21 or higher.  
   - Deploy to a physical Android device (with NFC) or emulator (requires NFC passthrough).  

2. **Permissions**  
   - The app requires `<uses-permission android:name="android.permission.NFC" />` in the generated manifest.  
   - Make sure NFC is enabled on your device.  

3. **Usage Overview**  
   - **Raw APDU Terminal**:  
     1. Launch the “NFC Terminal” page.  
     2. Type a hex APDU (e.g. `00A4040007A0000002471001`).  
     3. Tap **Send**.  
     4. View the card response.  
   - **Lin Interactive Terminal**:  
     1. Open “Lin Terminal.”  
     2. At the prompt, type `apdu("00A4040007A0000002471001")` → returns a hex string.  
     3. Try `print("Hello, NFC!")` or `printc("#00FF00", "OK")`.  
     4. Combine logic:  
        ```lin
        resp = apdu("00B0950000")
        if(find(resp, "9000") >= 0){
           printc("#00FF00", "Success:", resp)
        } else {
           error("Card returned error:", resp)
		}
        ```  
   - **Lin Script Builder**:  
     1. Navigate to “Script” page.  
     2. Write a multi-line script, for example:  
        ```lin
        // Select PSE
        sel = apdu("00A404000E315041592E5359532E4444463031")
        print("Select PSE", sel)
        
        // Parse TLV and display hierarchy
        t = tlv(sel)
        print(tree(t))
        
        // Read record 1
        rec = apdu("00B2010C00")
        print("Record 1", rec)
        ```  
     3. Tap **Run** to execute the entire script at once. Output appears below.  
   - **TLV Parser / Tree Viewer**:  
     1. Go to “TLV” page.  
     2. Paste a hex string (e.g. `6F10840A325041592E5359532E4444463031A5048801020046...`).  
     3. The app will automatically parse and display:  
        ```
        └─ 6F
           ├─ 84 : 325041592E5359532E4444463031
           └─ A5
              ├─ 88 : 010200
              └─ 46 : 6F6368657343617264
        ```  
     4. You can also pipe the APDU response from the Raw Terminal.

---

## License & Contributions

This project is released under the MIT License. Contributions are welcome:

- Fork the repository, make your changes, and open a pull request.  
- Report issues or feature requests via Issues on GitHub.  

Enjoy experimenting with NFC, APDUs, Lin scripting, and TLV parsing!  

## Used Free incos

[terminal](https://freeicons.io/apps-&-programming-2/applications-and-programming-application-coding-terminal-icon-41762)
[cdoing](https://freeicons.io/apps-&-programming-2/applications-and-programming-application-coding-web-code-write-icon-41764)
[tree](https://freeicons.io/interface-v.2/hierarchy-organized-diagram-structure-icon-105862)
[nfc](https://freeicons.io/payment-icon-set-35662/nfc-contactless-card-payment-mobile-banking-icon-1431369)
