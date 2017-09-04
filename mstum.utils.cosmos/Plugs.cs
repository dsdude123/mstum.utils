using System;
using System.Collections.Generic;
using System.Text;

namespace mstum.utils.cosmos
{
    public static class Plugs
    {
        public static string CosmosReverse(string input)
        {
            string output = null;
            for (int i = 1; i <= input.Length; i++)
            {
                output += input.Substring(input.Length - i, 1);
            }
            return output;
        }
    }
}
