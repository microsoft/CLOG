/*++

    Copyright (c) Microsoft Corporation.
    Licensed under the MIT License.

Abstract:

    Represents the node in the tree of type searches.
    
    For example if 
        %a
        %ab
        %aa
        %abc 

        you'd get a tree like


           a
          / \
         a   b 
              \
               c

--*/

using System.Collections.Generic;

namespace clogutils
{
    public class CLogTypeSearchNode
    {
        public Dictionary<char, CLogTypeSearchNode> Nodes = new Dictionary<char, CLogTypeSearchNode>();

        public CLogEncodingCLogTypeSearch UserNode { get; set; }

        private static void Flatten(CLogTypeSearchNode node, List<CLogEncodingCLogTypeSearch> ret)
        {
            if (null != node.UserNode)
                ret.Add(node.UserNode);

            foreach (var n in node.Nodes)
            {
                Flatten(n.Value, ret);
            }
        }

        public IEnumerable<CLogEncodingCLogTypeSearch> Flatten()
        {
            List<CLogEncodingCLogTypeSearch> ret = new List<CLogEncodingCLogTypeSearch>();
            Flatten(this, ret);
            return ret;
        }
    }
}
