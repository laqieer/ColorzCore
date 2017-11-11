﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorzCore.DataTypes
{
    public class MergeableGenerator<T>
    {
        private Stack<IEnumerator<T>> myEnums;
        public bool EOS { get; private set; }
        public MergeableGenerator(IEnumerable<T> baseEnum)
        {
            myEnums = new Stack<IEnumerator<T>>();
            myEnums.Push(baseEnum.GetEnumerator());
        }

        public T Current { get { return myEnums.Peek().Current; } }
        public bool MoveNext()
        {
            if(!myEnums.Peek().MoveNext())
            {
                if(myEnums.Count > 1)
                {
                    myEnums.Pop();
                    return true;
                }
                else
                {
                    EOS = true;
                    return false;
                }
            }
            else
            {
                return true;
            }
        }
        public void PrependEnumerator(IEnumerator<T> nextEnum)
        {
            if (EOS)
                myEnums.Pop();
            EOS = false;
            myEnums.Push(nextEnum);
            Prime();
        }
        public void PutBack(T elem)
        {
            this.PrependEnumerator(new List<T> { elem }.GetEnumerator());
        }
        private bool Prime()
        {
            return myEnums.Peek().MoveNext();
        }
        public IEnumerator<T> GetEnumerator()
        {
            while(!EOS) {
                yield return this.Current;
                this.MoveNext();
            }
        }
    }
}
