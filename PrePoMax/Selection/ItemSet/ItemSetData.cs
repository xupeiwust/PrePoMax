﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.ComponentModel;
using CaeGlobals;

namespace PrePoMax
{
    public enum ItemSetDataToStringType
    {
        NumberOfItems,
        SelectSinglePoint
    }

    public class ItemSetData
    {
        // Variables                                                                                                                
        protected int[] _itemIds;
        protected ItemSetDataToStringType _toStringType;


        // Properties                                                                                                               
        public int[] ItemIds { get { return _itemIds; } set { if (value != _itemIds) _itemIds = value; } }
        public ItemSetDataToStringType ToStringType { get { return _toStringType; } set { _toStringType = value; } }


        // Constructors                                                                                                             
        public ItemSetData()
            : this(null)
        {
        }
        public ItemSetData(int[] itemIds)
        {
            _itemIds = itemIds;
            _toStringType = ItemSetDataToStringType.NumberOfItems;
        }


        // Methods                                                                                                                  
        public override string ToString()
        {
            if (_toStringType == ItemSetDataToStringType.NumberOfItems)
            {
                if (_itemIds == null || _itemIds.Length == 0) return "Empty";
                else return "Number of items: " + _itemIds.Length;
            }
            else if (_toStringType == ItemSetDataToStringType.SelectSinglePoint)
            {
                return "Select a point";
            }
            else throw new NotSupportedException();
        }
    }
}