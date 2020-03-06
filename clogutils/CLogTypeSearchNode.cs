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
    }
}
