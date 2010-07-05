// 
// QueryParser.cs
//  
// Author:
//       Scott Thomas <lunchtimemama@gmail.com>
// 
// Copyright (c) 2010 Scott Thomas
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Text;

namespace Mono.Upnp.Dcp.MediaServer1.ContentDirectory1
{
    // Refer to ContentDirectory1 Service Template 1.0.1, Section 2.5.5.1: Search Criteria String Syntax
    public class QueryParser
    {
        Query last_expression;

        QueryParser ()
        {
        }

        protected virtual QueryParser OnCharacter (char character)
        {
            if (IsWhiteSpace (character)) {
                return this;
            } else {
                return new RootParser (token => {
                    if (token == "and") {
                        return null;
                    } else if (token == "or") {
                        return null;
                    } else {
                        return new RootExpressionParser (token, expression => {
                            last_expression = expression;
                            return this;
                        });
                    }
                }).OnCharacter (character);
            }
        }

        protected virtual Query OnDone ()
        {
            return last_expression;
        }

        protected bool IsWhiteSpace (char character)
        {
            switch (character) {
            case ' ': return true;
            case '\t': return true;
            case '\n': return true;
            case '\v': return true;
            case '\f': return true;
            case '\r': return true;
            default: return false;
            }
        }

        delegate QueryParser Consumer<T> (T value);

        abstract class ExpressionParser : QueryParser
        {
            protected readonly string Property;
            protected readonly Consumer<Query> Consumer;

            protected ExpressionParser (string property, Consumer<Query> consumer)
            {
                Property = property;
                Consumer = consumer;
            }
        }

        class RootExpressionParser : ExpressionParser
        {
            public RootExpressionParser (string property, Consumer<Query> consumer)
                : base (property, consumer)
            {
            }

            protected override QueryParser OnCharacter (char character)
            {
                if (IsWhiteSpace (character)) {
                    return this;
                }

                switch (character) {
                case '=': return new EqualityParser (Property, Consumer);
                case '!': return new InequalityParser (Property, Consumer);
                case '<': return new LessThanParser (Property, Consumer);
                case '>': return new GreaterThanParser (Property, Consumer);
                case 'c': return new ContainsParser (Property, Consumer).OnCharacter ('c');
                case 'e': return new ExistsParser (Property, Consumer).OnCharacter ('e');
                case 'd': return new DerivedFromOrDoesNotContainParser (Property, Consumer);
                default: throw new QueryParsingException (string.Format (
                    "Unrecognized operator begining: {0}.", character));
                }
            }
        }

        abstract class OperatorParser : ExpressionParser
        {
            protected bool Initialized;

            protected OperatorParser (string property, Consumer<Query> consumer)
                : base (property, consumer)
            {
            }

            protected override QueryParser OnCharacter (char character)
            {
                if (Initialized) {
                    if (character == '"') {
                        return GetOperandParser ();
                    } else if (IsWhiteSpace (character)) {
                        return this;
                    } else {
                        throw new QueryParsingException (string.Format (
                            "Expecting double-quoted string operand with the operator: {0}.", Operator));
                    }
                } else if (IsWhiteSpace (character)) {
                    Initialized = true;
                    return this;
                } else {
                    throw new QueryParsingException (string.Format (
                        "Whitespace is required around the operator: {0}.", Operator));
                }
            }

            protected abstract QueryParser GetOperandParser ();

            protected abstract string Operator { get; }

            protected override Query OnDone ()
            {
                throw new QueryParsingException (string.Format (
                    "There is no operand for the operator: {0}.", Operator));
            }
        }

        abstract class StringOperatorParser : OperatorParser
        {
            readonly string @operator;
            int position;

            protected StringOperatorParser (string @operator, string property, Consumer<Query> consumer)
                : base (property, consumer)
            {
                this.@operator = @operator;
            }

            protected override QueryParser OnCharacter (char character)
            {
                if (Initialized || position >= @operator.Length || character != @operator[position]) {
                    var parser = base.OnCharacter (character);
                    if (Initialized && position < @operator.Length) {
                        throw new QueryParsingException (string.Format (
                            "Unrecognized operator: {0}.", @operator.Substring (0, position)));
                    }
                    return parser;
                } else {
                    position++;
                    return this;
                }
            }

            protected override string Operator {
                get { return @operator; }
            }
        }

        class ContainsParser : StringOperatorParser
        {
            public ContainsParser (string property, Consumer<Query> consumer)
                : base ("contains", property, consumer)
            {
            }

            protected override QueryParser GetOperandParser ()
            {
                return new StringParser (value => Consumer (visitor => visitor.VisitContains (Property, value)));
            }
        }

        class ExistsParser : StringOperatorParser
        {
            public ExistsParser (string property, Consumer<Query> consumer)
                : base ("exists", property, consumer)
            {
            }

            protected override QueryParser OnCharacter (char character)
            {
                if (Initialized) {
                    return GetOperandParser ().OnCharacter (character);
                } else {
                    return base.OnCharacter (character);
                }
            }

            protected override QueryParser GetOperandParser ()
            {
                return new BooleanParser (value => Consumer (visitor => visitor.VisitExists (Property, value)));
            }
        }

        abstract class EqualityOperatorParser : OperatorParser
        {
            protected bool HasEqualsSign;

            protected EqualityOperatorParser (string property, Consumer<Query> consumer)
                : base (property, consumer)
            {
            }

            protected override QueryParser OnCharacter (char character)
            {
                if (HasEqualsSign || character != '=') {
                    return base.OnCharacter (character);
                } else {
                    HasEqualsSign = true;
                    return this;
                }
            }
        }

        class DerivedFromOrDoesNotContainParser : ExpressionParser
        {
            public DerivedFromOrDoesNotContainParser (string property, Consumer<Query> consumer)
                : base (property, consumer)
            {
            }

            protected override QueryParser OnCharacter (char character)
            {
                if (character == 'e') {
                    return new DerivedFromParser (Property, Consumer).OnCharacter ('d').OnCharacter ('e');
                } else  if (character == 'o') {
                    return new DoesNotContainParser (Property, Consumer).OnCharacter ('d').OnCharacter ('o');
                } else if (IsWhiteSpace (character)) {
                    throw new QueryParsingException ("Unrecognized operator: d.");
                } else {
                    throw new QueryParsingException (string.Format (
                        "Unrecognized operator begining: d{0}.", character));
                }
            }

            protected override Query OnDone ()
            {
                throw new QueryParsingException ("Unrecognized operator begining: d.");
            }
        }

        class DerivedFromParser : StringOperatorParser
        {
            public DerivedFromParser (string property, Consumer<Query> consumer)
                : base ("derivedFrom", property, consumer)
            {
            }

            protected override QueryParser GetOperandParser ()
            {
                return new StringParser (value => Consumer (visitor => visitor.VisitDerivedFrom (Property, value)));
            }
        }

        class DoesNotContainParser : StringOperatorParser
        {
            public DoesNotContainParser (string property, Consumer<Query> consumer)
                : base ("doesNotContain", property, consumer)
            {
            }

            protected override QueryParser GetOperandParser ()
            {
                return new StringParser (value => Consumer (visitor => visitor.VisitDoesNotContain (Property, value)));
            }
        }

        class EqualityParser : OperatorParser
        {
            public EqualityParser (string property, Consumer<Query> consumer)
                : base (property, consumer)
            {
            }

            protected override QueryParser GetOperandParser ()
            {
                return new StringParser (value => Consumer (visitor => visitor.VisitEquals (Property, value)));
            }

            protected override string Operator {
                get { return "="; }
            }
        }

        class InequalityParser : EqualityOperatorParser
        {
            public InequalityParser (string property, Consumer<Query> consumer)
                : base (property, consumer)
            {
            }

            protected override QueryParser OnCharacter (char character)
            {
                var parser = base.OnCharacter (character);
                if (Initialized && !HasEqualsSign) {
                    throw new QueryParsingException ("Incomplete operator: !=.");
                }
                return parser;
            }

            protected override QueryParser GetOperandParser ()
            {
                return new StringParser (value => Consumer (visitor => visitor.VisitDoesNotEqual (Property, value)));
            }

            protected override string Operator {
                get { return "!="; }
            }
        }

        class LessThanParser : EqualityOperatorParser
        {
            public LessThanParser (string property, Consumer<Query> consumer)
                : base (property, consumer)
            {
            }

            protected override QueryParser GetOperandParser ()
            {
                if (HasEqualsSign) {
                    return new StringParser (value => Consumer (visitor => visitor.VisitLessThanOrEqualTo (Property, value)));
                } else {
                    return new StringParser (value => Consumer (visitor => visitor.VisitLessThan (Property, value)));
                }
            }

            protected override string Operator {
                get { return HasEqualsSign ? "<=" : "<"; }
            }
        }

        class GreaterThanParser : EqualityOperatorParser
        {
            public GreaterThanParser (string property, Consumer<Query> consumer)
                : base (property, consumer)
            {
            }

            protected override QueryParser GetOperandParser ()
            {
                if (HasEqualsSign) {
                    return new StringParser (value => Consumer (visitor => visitor.VisitGreaterThanOrEqualTo (Property, value)));
                } else {
                    return new StringParser (value => Consumer (visitor => visitor.VisitGreaterThan (Property, value)));
                }
            }

            protected override string Operator {
                get { return HasEqualsSign ? ">=" : ">"; }
            }
        }

        class RootParser : QueryParser
        {
            readonly Consumer<string> consumer;
            StringBuilder builder = new StringBuilder ();

            public RootParser (Consumer<string> consumer)
            {
                this.consumer = consumer;
            }

            protected override QueryParser OnCharacter (char character)
            {
                if (IsWhiteSpace (character)) {
                    return consumer (builder.ToString ());
                } else {
                    builder.Append (character);
                    return this;
                }
            }

            protected override Query OnDone ()
            {
                throw new QueryParsingException (string.Format (
                    @"The identifier is not a part of an expression: {0}.", builder.ToString ()));
            }
        }

        class StringParser : QueryParser
        {
            readonly Consumer<string> consumer;
            StringBuilder builder = new StringBuilder ();
            bool escaped;

            public StringParser (Consumer<string> consumer)
            {
                this.consumer = consumer;
            }

            protected override QueryParser OnCharacter (char character)
            {
                if (escaped) {
                    if (character == '\\') {
                        builder.Append ('\\');
                    } else  if (character == '"') {
                        builder.Append ('"');
                    } else {
                        throw new QueryParsingException ("Unrecognized escape sequence: \\" + character);
                    }
                    escaped = false;
                    return this;
                } else if (character == '\\') {
                    escaped = true;
                    return this;
                } else if (character == '"') {
                    return consumer (builder.ToString ());
                } else {
                    builder.Append (character);
                    return this;
                }
            }

            protected override Query OnDone ()
            {
                throw new QueryParsingException (string.Format (
                    @"The double-quoted string is not terminated: ""{0}"".", builder.ToString ()));
            }
        }

        class BooleanParser : QueryParser
        {
            readonly Consumer<bool> consumer;
            bool @true;
            int position;

            public BooleanParser (Consumer<bool> consumer)
            {
                this.consumer = consumer;
            }

            protected override QueryParser OnCharacter (char character)
            {
                if (position == 0) {
                    if (IsWhiteSpace (character)) {
                        return this;
                    } else if (character == 't') {
                        @true = true;
                        return Check ("true", character);
                    } else if (character == 'f') {
                        return Check ("false", character);
                    } else {
                        return Fail<QueryParser> ();
                    }
                } else if (@true) {
                    return Check ("true", character);
                } else {
                    return Check ("false", character);
                }
            }

            protected override Query OnDone ()
            {
                if (position == 0) {
                    return Fail<Query> ();
                } else if (@true) {
                    if (position == "true".Length) {
                        return consumer (true).OnDone ();
                    } else {
                        return Fail<Query> ();
                    }
                } else {
                    if (position == "false".Length) {
                        return consumer (false).OnDone ();
                    } else {
                        return Fail<Query> ();
                    }
                }
            }

            QueryParser Check (string expected, char character)
            {
                if (position == expected.Length) {
                    if (IsWhiteSpace (character)) {
                        return consumer (@true);
                    } else if (character == ')' || character == '(') {
                        return consumer (@true).OnCharacter (character);
                    } else {
                        return Fail<QueryParser> ();
                    }
                } else if (expected[position] == character) {
                    position++;
                    return this;
                } else {
                    return Fail<QueryParser> ();
                }
            }

            T Fail<T> ()
            {
                throw new QueryParsingException (@"Expecting either ""true"" or ""false"".");
            }
        }

        public static Query Parse (string query)
        {
            var parser = new QueryParser ();
            foreach (var character in query) {
                parser = parser.OnCharacter (character);
            }
            return parser.OnDone ();
        }
    }
}
