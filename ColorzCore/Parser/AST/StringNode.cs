﻿using ColorzCore.DataTypes;
using ColorzCore.Lexer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorzCore.Parser.AST
{
    public class StringNode : IParamNode
    {
        private Token myToken;

        public Location MyLocation { get { return myToken.Location; } }
        public ParamType Type { get { return ParamType.STRING; } }

        public StringNode(Token value)
        {
            myToken = value;
        }

        public byte[] ToBytes()
        {
            return Encoding.ASCII.GetBytes(ToString());
        }

        public override string ToString()
        {
            return myToken.Content;
        }
        public string PrettyPrint()
        {
            return '"' + ToString() + '"';
        }
        public IEnumerable<Token> ToTokens() { yield return myToken; }
    }
}
