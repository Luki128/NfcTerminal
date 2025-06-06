using System.Globalization;
using System.Text;

namespace Lin
{
    /// <summary>
    /// A very small expression‐language interpreter featuring variables, arithmetic/bitwise operators,
    /// user‑defined functions and simple control flow constructs (<c>if</c>, <c>for</c>, <c>return</c>).
    /// </summary>
    public class LinInterpreter
    {
        // ---------------------------------------------------------------------
        //  Storage
        // ---------------------------------------------------------------------

        /// <summary>Holds the values of variables declared during the interactive session.</summary>
        private readonly Dictionary<string, object> variableStorage = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Holds the bodies of user‑defined functions (<c>fun</c> keyword).</summary>
        private readonly Dictionary<string, UserFunction> codedFunctions = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Accumulates multi‑line input until braces are balanced – used for composite statements.</summary>
        private readonly StringBuilder inputBuffer = new();

        /// <summary>
        /// Registry of built‑in functions.  You can extend it by adding new entries to the dictionary.
        /// </summary>
        private readonly Dictionary<string, Func<List<object>, object>> buildinFunctions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["max"] = args => Math.Max(ToLong(args[0]), ToLong(args[1])),
                ["min"] = args => Math.Min(ToLong(args[0]), ToLong(args[1])),
                ["len"] = args => args[0]?.ToString()?.Length ?? 0,
                ["print"] = args => { Console.WriteLine(args[0]?.ToString()); return ""; },
            };



        // =====================================================================
        #region Public API
        // =====================================================================

        /// <summary>
        /// Executes a single line or a multi‑line compound statement.  An expression returns its value;
        /// assignments return the assigned value; statements that do not produce a value return <c>null</c>.
        /// </summary>
        /// <param name="line">A line read from the REPL.</param>
        /// <returns>The result of the executed line or <c>null</c>.</returns>
        public object Execute(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            var inputCopy = inputBuffer.ToString();

            try
            {

                // Collect text until all braces are balanced: { … }
                inputBuffer.AppendLine(line);
                if (!BracesBalanced(inputBuffer.ToString()))
                    return null;                       // wait for more lines

                tokens = Tokenize(inputBuffer.ToString());
                inputBuffer.Clear();
                tokenLocation = 0;

                object result = ParseStatement();      // starting point of the parser
                Expect(TokenType.EOF);
                return result;
            }
            catch (Exception ex) // handle fail to clear buffer
            {
                inputBuffer.Clear();
                inputBuffer.Append(inputCopy);
                throw ex;
            }
            return null;
        }

        /// <summary>Read‑only accessor for a variable.  Returns <c>null</c> when not defined.</summary>
        public object this[string varName] => variableStorage.TryGetValue(varName, out var v) ? v : null;


        /// <summary>
        /// Set or add, build-in function to language
        /// </summary>
        /// <param name="name">name of build in function</param>
        /// <param name="function">function itself</param>
        public void SetFunction(string name, Func<List<object>, object> function)
          => buildinFunctions[name] = function;


        #endregion

        // =====================================================================
        #region Lexer
        // =====================================================================

        private enum TokenType
        {
            Number, String, Identifier, Operator,
            Comma, Semicolon,
            LParen, RParen, LBrace, RBrace,
            Assign, EOF
        }

        private record Token(TokenType Type, string Text);

        /// <summary>
        /// Converts raw source text into a flat list of tokens.
        /// </summary>
        private static List<Token> Tokenize(string input)
        {
            var result = new List<Token>();
            int i = 0;

            while (i < input.Length)
            {
                char c = input[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                // String literal
                if (c == '"')
                {
                    int start = ++i;
                    while (i < input.Length && input[i] != '"') i++;
                    result.Add(new(TokenType.String, input[start..i]));
                    i++; continue;
                }

                // Identifier
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_')) i++;
                    result.Add(new(TokenType.Identifier, input[start..i]));
                    continue;
                }

                // Decimal or hexadecimal number
                if (char.IsDigit(c))
                {
                    int start = i;
                    if (c == '0' && i + 1 < input.Length && (input[i + 1] is 'x' or 'X'))
                    {
                        i += 2;
                        while (i < input.Length && IsHexDigit(input[i])) i++;
                    }
                    else
                    {
                        while (i < input.Length && char.IsDigit(input[i])) i++;
                    }
                    result.Add(new(TokenType.Number, input[start..i]));
                    continue;
                }

                // Two‑character operators need to be checked first
                string op2 = i + 1 < input.Length ? input.Substring(i, 2) : null;
                if (op2 is "==" or "<<" or ">>" or "<=" or ">=" or "!=")
                {
                    result.Add(new(TokenType.Operator, op2)); i += 2; continue;
                }

                // Single‑character tokens
                if (c == '=') { result.Add(new(TokenType.Assign, "=")); i++; continue; }
                if (c == ',') { result.Add(new(TokenType.Comma, ",")); i++; continue; }
                if (c == '(') { result.Add(new(TokenType.LParen, "(")); i++; continue; }
                if (c == ')') { result.Add(new(TokenType.RParen, ")")); i++; continue; }
                if (c == ';') { result.Add(new(TokenType.Semicolon, ";")); i++; continue; }
                if (c == '{') { result.Add(new(TokenType.LBrace, "{")); i++; continue; }
                if (c == '}') { result.Add(new(TokenType.RBrace, "}")); i++; continue; }
                if ("+-*/%|&^<>".IndexOf(c) >= 0)
                {
                    result.Add(new(TokenType.Operator, c.ToString())); i++; continue;
                }

                throw new Exception($"Unknown symbol: '{c}'.");
            }

            result.Add(new(TokenType.EOF, string.Empty));
            return result;

            static bool IsHexDigit(char ch) =>
                char.IsDigit(ch) || (ch is >= 'a' and <= 'f') || (ch is >= 'A' and <= 'F');
        }

        #endregion

        // =====================================================================
        #region Parser / Evaluator
        // =====================================================================

        private List<Token> tokens;
        private int tokenLocation;
        private record UserFunction(string[] Parameters, List<Token> Body);

        private bool LookAhead(TokenType type, int offset = 0) =>
            tokenLocation + offset < tokens.Count && tokens[tokenLocation + offset].Type == type;

        private void Optional(TokenType t) { if (LookAhead(t)) tokenLocation++; }

        // ------------------------------------------------------------------
        //  Statements
        // ------------------------------------------------------------------

        private object ParseStatement(bool tolerateSemicolon = true)
        {
            // fun …
            if (LookAhead(TokenType.Identifier) &&
                tokens[tokenLocation].Text.Equals("fun", StringComparison.OrdinalIgnoreCase))
                return ParseFunDefinition();

            // for …
            if (LookAhead(TokenType.Identifier) &&
                tokens[tokenLocation].Text.Equals("for", StringComparison.OrdinalIgnoreCase))
                return ExecForLoop();

            // if …
            if (LookAhead(TokenType.Identifier) &&
                tokens[tokenLocation].Text.Equals("if", StringComparison.OrdinalIgnoreCase))
                return ExecIfStatement();

            // return …
            if (LookAhead(TokenType.Identifier) &&
                tokens[tokenLocation].Text.Equals("return", StringComparison.OrdinalIgnoreCase))
            {
                tokenLocation++;                       // consume "return"
                object val = ParseExpression();
                if (tolerateSemicolon)
                    Optional(TokenType.Semicolon);
                throw new ReturnException(val);
            }

            // assignment
            if (LookAhead(TokenType.Identifier) && LookAhead(TokenType.Assign, 1))
            {
                string name = tokens[tokenLocation].Text;
                tokenLocation += 2;                    // id '='
                object val = ParseExpression();
                variableStorage[name] = val;
                if (tolerateSemicolon)
                    Optional(TokenType.Semicolon);
                return val;
            }

            // plain expression
            object value = ParseExpression();
            if (tolerateSemicolon)
                Optional(TokenType.Semicolon);
            return value;
        }

        // ------------------------------------------------------------------
        //  if … else
        // ------------------------------------------------------------------

        private object ExecIfStatement()
        {
            tokenLocation++;                           // consume "if"
            Consume(TokenType.LParen);
            object cond = ParseExpression();
            Consume(TokenType.RParen);

            var thenBody = CaptureBlock();             // { … }

            List<Token> elseBody = null;
            if (LookAhead(TokenType.Identifier) &&
                tokens[tokenLocation].Text.Equals("else", StringComparison.OrdinalIgnoreCase))
            {
                tokenLocation++;                       // consume "else"
                elseBody = CaptureBlock();
            }

            if (IsTrue(cond))
                ExecuteBlock(thenBody);
            else if (elseBody is not null)
                ExecuteBlock(elseBody);

            return "(if ok)";
        }

        // ------------------------------------------------------------------
        //  Helpers – token consumption
        // ------------------------------------------------------------------

        private Token Consume(TokenType expected)
        {
            var tok = tokens[tokenLocation];
            if (tok.Type != expected)
                throw new Exception($"Expected {expected}, but found {tok.Type}.");
            tokenLocation++; return tok;
        }

        private Token ConsumeUntil(TokenType expected)
        {
            var tok = tokens[tokenLocation];

            while (tokenLocation < tokens.Count && tok.Type != expected)
                tok = tokens[++tokenLocation];

            if (tok.Type != expected)
                throw new Exception($"Missing expected {expected}, found {tok.Type}.");
            tokenLocation++;
            return tok;
        }

        private void Expect(TokenType t) => Consume(t);

        // ------------------------------------------------------------------
        //  Expression grammar – precedence levels 0‑8
        // ------------------------------------------------------------------
        // 0: () / number / identifier / funcCall
        // 1: unary + -
        // 2: * / %
        // 3: + -
        // 4: << >>
        // 5: < <= > >= == !=
        // 6: &
        // 7: ^
        // 8: |

        private object ParseExpression(int prec = 8)
        {
            if (prec == 0)
                return ParsePrimary();

            object left = ParseExpression(prec - 1);

            while (true)
            {
                if (!IsOperatorWithPrecedence(tokens[tokenLocation], prec))
                    break;
                string op = tokens[tokenLocation].Text; tokenLocation++;
                object right = ParseExpression(prec - 1);
                left = ApplyOperator(op, left, right);
            }
            return left;
        }

        private object ParsePrimary()
        {
            Token tok = tokens[tokenLocation];

            switch (tok.Type)
            {
                case TokenType.Number:
                    tokenLocation++;
                    return ParseNumber(tok.Text);

                case TokenType.String:
                    tokenLocation++;
                    return tok.Text;

                case TokenType.Identifier:
                    tokenLocation++;
                    if (LookAhead(TokenType.LParen))         // function call
                        return ParseFuncCall(tok.Text);
                    return variableStorage.TryGetValue(tok.Text, out var val)
                        ? val
                        : throw new Exception($"Undefined variable '{tok.Text}'.");

                case TokenType.LParen:
                    tokenLocation++;
                    object inside = ParseExpression();
                    Consume(TokenType.RParen);
                    return inside;

                default:
                    throw new Exception($"Unexpected token: {tok.Type}.");
            }
        }

        private object ParseFuncCall(string name)
        {
            Consume(TokenType.LParen);
            var args = new List<object>();
            if (!LookAhead(TokenType.RParen))
            {
                args.Add(ParseExpression());
                while (LookAhead(TokenType.Comma))
                {
                    tokenLocation++;      // consume ','
                    args.Add(ParseExpression());
                }
            }
            Consume(TokenType.RParen);

            // user‑defined function?
            if (codedFunctions.TryGetValue(name, out var uf))
            {
                if (args.Count != uf.Parameters.Length)
                    throw new Exception($"Function '{name}' requires {uf.Parameters.Length} argument(s).");
                var locals = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < args.Count; i++) locals[uf.Parameters[i]] = args[i];
                return ExecuteUserFunction(uf, locals);
            }

            // built‑in function?
            if (!buildinFunctions.TryGetValue(name, out var fn))
                throw new Exception($"Built‑in function '{name}' not found.");
            return fn(args);
        }

        private static bool IsOperatorWithPrecedence(Token tok, int prec) => tok.Type == TokenType.Operator && tok.Text switch
        {
            "*" or "/" or "%" => prec == 2,
            "+" or "-" => prec == 3,
            "<<" or ">>" => prec == 4,
            "<" or ">" or "<=" or ">=" or "==" or "!=" => prec == 5,
            "&" => prec == 6,
            "^" => prec == 7,
            "|" => prec == 8,
            _ => false
        };

        private static object ApplyOperator(string op, object a, object b)
        {
            // String concatenation is only allowed with '+'.
            if (a is string || b is string)
            {
                string xs = $"{a}", ys = $"{b}";
                return op switch
                {
                    "+" => xs + ys,
                    "==" => (long)(xs == ys ? 1 : 0),
                    "!=" => (long)(xs != ys ? 1 : 0),
                    _ => throw new Exception($"Unsupported operator '{op} for strings'.")
                };
            }

            long x = ToLong(a), y = ToLong(b);

            return op switch
            {
                "+" => x + y,
                "-" => x - y,
                "*" => x * y,
                "/" => y == 0 ? throw new DivideByZeroException() : x / y,
                "%" => y == 0 ? throw new DivideByZeroException() : x % y,
                "|" => x | y,
                "&" => x & y,
                "^" => x ^ y,
                "<<" => x << (int)y,
                ">>" => x >> (int)y,
                "<" => x < y ? 1 : 0,
                ">" => x > y ? 1 : 0,
                "<=" => x <= y ? 1 : 0,
                ">=" => x >= y ? 1 : 0,
                "==" => x == y ? 1 : 0,
                "!=" => x != y ? 1 : 0,
                _ => throw new Exception($"Unsupported operator '{op}'.")
            };
        }

        private static object ParseNumber(string text)
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Convert.ToInt64(text[2..], 16);
            return long.Parse(text, CultureInfo.InvariantCulture);
        }

        private static long ToLong(object o) =>
            o switch
            {
                long l => l,
                int i => i,
                _ => throw new Exception("A numeric value is required.")
            };

        // ------------------------------------------------------------------
        //  User‑defined functions
        // ------------------------------------------------------------------

        private object ParseFunDefinition()
        {
            tokenLocation++;                           // consume "fun"
            string name = Consume(TokenType.Identifier).Text;

            Consume(TokenType.LParen);
            var args = new List<string>();
            if (!LookAhead(TokenType.RParen))
            {
                args.Add(Consume(TokenType.Identifier).Text);
                while (LookAhead(TokenType.Comma)) { tokenLocation++; args.Add(Consume(TokenType.Identifier).Text); }
            }
            Consume(TokenType.RParen);

            var body = CaptureBlock();
            codedFunctions[name] = new(args.ToArray(), body);
            return "(fun ok)";
        }

        // ------------------------------------------------------------------
        //  for (…) { … }
        // ------------------------------------------------------------------

        private object ExecForLoop()
        {
            tokenLocation++;                           // consume "for"
            Consume(TokenType.LParen);

            // init
            ParseStatement(false); Consume(TokenType.Semicolon);

            int condPos = tokenLocation;               // beginning of the condition
            ParseExpression(); Consume(TokenType.Semicolon);

            int postPos = tokenLocation;               // beginning of the post expression

            ConsumeUntil(TokenType.RParen);

            var body = CaptureBlock();
            int retPos = tokenLocation;

            while (true)
            {
                // evaluate condition
                tokenLocation = condPos;
                object cond = ParseExpression();
                if (!IsTrue(cond)) break;

                // execute body
                ExecuteBlock(body);

                // post‑expression
                tokenLocation = postPos;
                ParseStatement(false);
            }
            tokenLocation = retPos;

            return "(for ok)";
        }

        private static bool IsTrue(object v) => v switch { long l => l != 0, _ => v is not null };

        // ------------------------------------------------------------------
        //  Function execution + return
        // ------------------------------------------------------------------

        private object ExecuteUserFunction(UserFunction uf, Dictionary<string, object> locals)
        {
            var saved = new Dictionary<string, object>(variableStorage, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in locals) variableStorage[kv.Key] = kv.Value;
            object ret = null;
            try { ExecuteBlock(uf.Body); }
            catch (ReturnException r) { ret = r.Value; }
            finally { variableStorage.Clear(); foreach (var kv in saved) variableStorage[kv.Key] = kv.Value; }
            return ret;
        }

        private class ReturnException : Exception { public readonly object Value; public ReturnException(object v) => Value = v; }

        // ------------------------------------------------------------------
        //  Block helpers
        // ------------------------------------------------------------------

        private void ExecuteBlock(List<Token> body)
        {
            var savedTokens = tokens; var savedPos = tokenLocation;
            tokens = body; tokenLocation = 0;
            while (!LookAhead(TokenType.EOF)) ParseStatement();
            tokens = savedTokens; tokenLocation = savedPos;
        }

        private List<Token> CaptureBlock()
        {
            Consume(TokenType.LBrace);
            int start = tokenLocation, level = 1;
            while (level > 0)
            {
                if (tokens[tokenLocation].Type == TokenType.LBrace) level++;
                else if (tokens[tokenLocation].Type == TokenType.RBrace) level--;
                tokenLocation++;
            }
            int end = tokenLocation - 1;                       // exclude closing '}'
            var slice = tokens.GetRange(start, end - start);
            slice.Add(new Token(TokenType.EOF, string.Empty));
            return slice;
        }

        private static bool BracesBalanced(string s)
        {
            int lvl = 0;
            foreach (char ch in s) if (ch == '{') lvl++; else if (ch == '}') lvl--;
            return lvl == 0;
        }

        #endregion
    }
}
