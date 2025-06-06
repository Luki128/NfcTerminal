using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using Lin;
using Libs.TLV;

namespace NfcDemo.Lin
{

    /// <summary>
    /// Provides extension functions for the LIN interpreter, including NFC operations and TLV parsing.
    /// </summary>
    public class LinExtension
    {
        /// <summary>
        /// The underlying LIN interpreter instance.
        /// </summary>
        private LinInterpreter interpreter;
        /// <summary>
        /// The page context in which commands are executed.
        /// </summary>
        private Page page;
        /// <summary>
        /// The NFC service used for APDU transceive operations.
        /// </summary>
        private INfcService nfc;
        /// <summary>
        /// Callback invoked when a message is printed, with a specified text and color.
        /// </summary>
        private Action<string, Color> onMessage;

        /// <summary>
        /// Cancellation token source for asynchronous operations.
        /// </summary>
        public CancellationTokenSource? cts;



        //[TODO] this should be in Lin in future
        /// <summary>
        /// Validates a list of arguments against expected type requirements.
        /// </summary>
        /// <param name="args">The list of argument objects to validate.</param>
        /// <param name="argsReqirmtns">
        /// A comma-separated string of rules, where each rule is one of:
        /// "i" (int64, required), "s" (string, required), "*" (any, required),
        /// "i*" (int64, optional), "s*" (string, optional), "**" (any, optional).
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown if an argument is missing when required, or if an argument's type does not match the expected type.
        /// </exception>
        private void ValiateArgs(List<object> args,string argsReqirmtns)
        {
            var rules = argsReqirmtns.Split(',');
            var map = new Dictionary<string, (string type, bool isOptional)>()
            {
                ["i"] = ("int64", false),
                ["s"] = ("string", false),
                ["*"] = ("any", false),
                ["i*"] = ("int64*", true),
                ["s*"] = ("string*", true),       
                ["**"] = ("any*", true),
            };
            for (int i = 0;i < rules.Count(); i++)
            {
                if (i >= args.Count())
                {
                    if (map[rules[i]].isOptional)
                        return;
                    throw new ArgumentException($"Insufficient amount of arguments excepted:{rules.Length} ({string.Join(',', rules.Select(x => map[x].type))})");
                }
                if (rules[i].StartsWith("*"))// any
                    continue;//valid
                if (rules[i].StartsWith("i") && (args[i] is int || args[i] is long))
                    continue;//valid
                if (rules[i].StartsWith("s") && args[i] is string)
                    continue;//valid
               
                throw new ArgumentException($"Unexpected type on argument {i + 1} provided {args[i].GetType().Name} required {map[rules[i]].type}");
            }
        }
        
        //[TODO] this should be in Lin in future
        private int GetInt(List<object> args,int idx) => (int)((long)args[idx]);



        /// <summary>
        /// Initializes a new instance of the <see cref="LinExtension"/> class.
        /// Also contains definitions for new built-in functions (data processing,nfc,printing):
        ///     adpu(hex)
        ///     num(string,base)
        ///     hex(int,counitng zeros)
        ///     ascii(string) -> hex byres to asci
        ///     substr(string,int bgin,count)
        ///     replace(str,form,to)
        ///     find(str,key)
        ///     byte(string,index,count*)
        ///     tlv(string)
        ///     tree(TLV)
        ///     tlv_read(tlv,string,*)
        /// </summary>
        /// <param name="page">The page context in which to execute LIN commands.</param>
        /// <param name="nfc">The NFC service used for sending and receiving APDU commands.</param>
        /// <param name="onMessage">Callback invoked when printing messages, with text and color.</param>

        public LinExtension(Page page, INfcService nfc, Action<string, Color> onMessage)
        {
            interpreter = new LinInterpreter();
            this.page = page;
            this.nfc = nfc;
            this.onMessage = onMessage;

            //adpu(hex)
            //num(string,base)
            //hex(int,counitng zeros)
            //ascii(string) -> hex byres to asci
            //substr(string,int bgin,count)
            //replace(str,form,to)
            //find(str,key)
            //byte(string,index,count*)
            //tlv(string)
            //tree(TLV)
            //tlv_read(tlv,string,*)


            interpreter.SetFunction("print", artgs =>
            {
                onMessage?.Invoke(string.Join(',',artgs.Select(x => x.ToString())),Colors.White); 
                return null;
            });
            interpreter.SetFunction("error", artgs =>
            {
                onMessage?.Invoke(string.Join(',', artgs.Select(x => x.ToString())), Colors.Violet);
                throw new Exception(string.Join(',', artgs.Select(x => x.ToString())));
            });
            interpreter.SetFunction("printc", artgs =>
            {
                ValiateArgs(artgs, "s,*");
                onMessage?.Invoke(string.Join(',', artgs.Skip(1).Select(x => x.ToString())), Color.FromHex((string)artgs[0]));
                return null;
            });
            interpreter.SetFunction("apdu", artgs =>
            {
                ValiateArgs(artgs, "s");
                byte[] resp = new byte[0];
                Task.Run(async () =>
                {
                    resp = await nfc.TransceiveAsync(TlvParser.HexStringToBytes(artgs[0] as string), cts.Token);
                }).Wait();
                var (data,sw) = Apdu.SplitResponse(resp);
                return $"{sw:X4}\t{BitConverter.ToString(data)}";
            });
            interpreter.SetFunction("num", artgs => {
                    ValiateArgs(artgs, "s,i*");
                    return Convert.ToInt64((string)artgs[0], artgs.Count() > 1 ? GetInt(artgs,1) : 10);
            });
            interpreter.SetFunction("hex", artgs => {
                ValiateArgs(artgs, "*,s*");
                if (artgs[0] is string)
                    return BitConverter.ToString(Encoding.ASCII.GetBytes((string)artgs[0])).Replace("-", " ");
                return ((long)artgs[0]).ToString("X" + ((artgs.Count() > 1 && artgs[1] is string) ? artgs[1] : ""));
            });
            interpreter.SetFunction("ascii", artgs => {
                ValiateArgs(artgs, "s");
                return Encoding.ASCII.GetString(TlvParser.HexStringToBytes(artgs[0] as string));
            });
            interpreter.SetFunction("substr", artgs => {
                ValiateArgs(artgs, "s,i,i");
                return (artgs[0] as string).Substring(GetInt(artgs, 1), GetInt(artgs, 2));
            });
            interpreter.SetFunction("replace", artgs => {
                ValiateArgs(artgs, "s,s,s");
                return (artgs[0] as string).Replace((string)artgs[1], (string)artgs[2]);
            });
            interpreter.SetFunction("find", artgs => {
                ValiateArgs(artgs, "s,s");
                return (artgs[0] as string).IndexOf((string)artgs[1]);
            });
            interpreter.SetFunction("bytes", artgs => {
                ValiateArgs(artgs, "s,i,i*");
                int count = artgs.Count() > 2 ? GetInt(artgs, 2) : 1;
                string cleaned = string.Concat(((string)artgs[0]).Where(c => !(char.IsWhiteSpace(c) || c == '-')));
                return string.Concat(cleaned.Skip(GetInt(artgs, 1) * 2).Take(count * 2));
            });
            interpreter.SetFunction("tlv", artgs => {
                ValiateArgs(artgs, "s");             
                return new TlvObject(TlvParser.Parse((string)artgs[0]));
            });
            interpreter.SetFunction("tree", artgs => {
                ValiateArgs(artgs, "*");
                if (artgs[0] is not TlvObject)
                    throw new ArgumentException($"Excepted {typeof(TlvObject).FullName} as argument 1");
                return  TreePrinter.GenerateTreeString(((TlvObject)artgs[0]).Source);
            });
            interpreter.SetFunction("tlv_read", artgs => {
                ValiateArgs(artgs, "*,s");
                if (artgs[0] is not TlvObject)
                    throw new ArgumentException($"Excepted {typeof(TlvObject).FullName} as argument 1");
                var tlv = (TlvObject)artgs[0];
                
                for (int i = 1; i < artgs.Count(); i++)
                {
                    if(tlv.Value != null || tlv.IsEmpty)
                        throw new IndexOutOfRangeException($"Tree ended on key: {(string)artgs[i-1]} key {(string)artgs[i]} not exist");
                    tlv = tlv[(string)artgs[i]];
                }
                if (tlv.Value != null)
                    return tlv.Value;
                return tlv;
            });
        }


        //support for comment in future lin too

        /// <summary>
        /// Executes a single line of LIN script, ignoring lines that start with "//" (comments).
        /// </summary>
        /// <param name="line">The line of LIN script to execute.</param>
        /// <returns>
        /// The result of the LIN interpreter execution, or <c>null</c> if the line is a comment or there is no return value.
        /// </returns>
        public object Execute(string line) => line.StartsWith("//") ? null : interpreter.Execute(line);
    }
}
