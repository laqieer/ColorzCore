﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorzCore.DataTypes
{
    public class ImmutableStack<T> : IEnumerable<T>
    {
        private Maybe<Tuple<T, ImmutableStack<T>>> member;
        bool cachedCount = false;
        int count;

        public ImmutableStack(T elem, ImmutableStack<T> tail)
        { member = new Just<Tuple<T,ImmutableStack<T>>>(new Tuple<T, ImmutableStack<T>>(elem, tail)); }
        private ImmutableStack()
        {
            member = new Nothing<Tuple<T, ImmutableStack<T>>>();
            cachedCount = true;
            count = 0;
        }

        private static ImmutableStack<T> emptyList = new ImmutableStack<T>();
        public static ImmutableStack<T> Nil { get { return emptyList;} }

        public bool IsEmpty { get { return member.IsNothing; } }
        public T Head { get { return member.FromJust.Item1; } }
        public ImmutableStack<T> Tail { get { return member.FromJust.Item2; } }
        public int Count { get
            {
                if (cachedCount)
                    return count;
                else
                {
                    count = Tail.Count + 1;
                    cachedCount = true;
                    return count;
                }
            } }
        public bool Contains(T toLookFor)
        {
            bool acc = false;
            for(ImmutableStack<T> temp = this; !acc && !temp.IsEmpty; temp = temp.Tail) acc |= temp.Head.Equals(toLookFor);
            return acc;
        }

        public IEnumerator<T> GetEnumerator()
        {
            ImmutableStack<T> temp = this;
            while(!temp.IsEmpty)
            {
                yield return temp.Head;
                temp = temp.Tail;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            ImmutableStack<T> temp = this;
            while (!temp.IsEmpty)
            {
                yield return temp.Head;
                temp = temp.Tail;
            }
        }
    }
}
