﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorzCore.Parser.AST
{
    interface IParamNode : IASTNode
    {
        string ToString(); //For use in other programs.
        int[] ToInts();
        byte[] ToBytes();
    }
}
