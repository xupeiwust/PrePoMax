﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaeMesh
{
    [Serializable]
    class CellEdgeData
    {
        public int[] NodeIds;
        public List<int> CellIds;
    }
}
