﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using CaeGlobals;
using DynamicTypeDescriptor;

namespace UserControls
{
    [Serializable]
    public class ViewSection
    {
        // Variables                                                                                                                
        private CaeModel.Section _section;
        protected DynamicCustomTypeDescriptor _dctd = null;


        // Properties                                                                                                               

        [CategoryAttribute("Data")]
        [ReadOnly(false)]
        [OrderedDisplayName(0, 10, "Name")]
        [DescriptionAttribute("Name of the section.")]
        public string Name { get { return _section.Name; } set { _section.Name = value; } }

        [CategoryAttribute("Data")]
        [OrderedDisplayName(1, 10, "Material")]
        [DescriptionAttribute("Select the material for the section definition.")]
        public string MaterialName { get { return _section.MaterialName; } set { _section.MaterialName = value; } }

        [OrderedDisplayName(2, 10, "Region type")]
        [CategoryAttribute("Data")]
        [DescriptionAttribute("Select the region type which will be used for the section definition.")]
        public string RegionType
        {
            get
            {
                if (_section.RegionType == RegionTypeEnum.PartName) return RegionTypeEnum.PartName.ToFriendlyString();
                else if (_section.RegionType == RegionTypeEnum.ElementSetName) return RegionTypeEnum.ElementSetName.ToFriendlyString();
                else throw new NotSupportedException();
            }
            set
            {
                CustomPropertyDescriptor cpd;
                if (value == RegionTypeEnum.PartName.ToFriendlyString())
                {
                    _section.RegionType = RegionTypeEnum.PartName;
                    cpd = _dctd.GetProperty("PartName");
                    _section.RegionName = cpd.StatandardValues.First().ToString();
                }
                else if (value == RegionTypeEnum.ElementSetName.ToFriendlyString())
                {
                    _section.RegionType = RegionTypeEnum.ElementSetName;
                    cpd = _dctd.GetProperty("ElementSetName");
                    _section.RegionName = cpd.StatandardValues.First().ToString();
                }
                else throw new NotSupportedException();

                UpdateRegionVisibility();
            }
        }

        [OrderedDisplayName(3, 10, "Part")]
        [CategoryAttribute("Data")]
        [DescriptionAttribute("Select the part which will be used for the section definition.")]
        public string PartName { get { return _section.RegionName; } set { _section.RegionName = value; } }

        [OrderedDisplayName(4, 10, "Element set")]
        [CategoryAttribute("Data")]
        [DescriptionAttribute("Select the element set which will be used for the section definition.")]
        public string ElementSetName { get { return _section.RegionName; } set { _section.RegionName = value; } }


        // Constructors                                                                                                             
        public ViewSection()
        {
        }

        // Methods
        public void SetBase(CaeModel.Section section)
        {
            _section = section;
            _dctd = ProviderInstaller.Install(this);
        }
        public CaeModel.Section GetBase()
        {
            return _section;
        }
        public void PopululateDropDownLists(string[] materialNames, string[] partNames, string[] elementSetNames)
        {
            _dctd.PopulateProperty(() => this.MaterialName, materialNames);
            _dctd.PopulateProperty(() => this.PartName, partNames);
            _dctd.PopulateProperty(() => this.ElementSetName, elementSetNames);

            // Prepare the region drop down list
            PopululateDropDownListForRegionType(partNames, elementSetNames);

            // Update visible dorp down lists
            UpdateRegionVisibility();
        }
        private void PopululateDropDownListForRegionType(string[] partNames, string[] elementSetNames)
        {
            List<string> types = new List<string>();
            if (partNames.Length > 0) types.Add(RegionTypeEnum.PartName.ToFriendlyString());
            if (elementSetNames.Length > 0) types.Add(RegionTypeEnum.ElementSetName.ToFriendlyString());

            _dctd.PopulateProperty(() => this.RegionType, types.ToArray(), false, 2);
        }
        public void UpdateRegionVisibility()
        {
            if (_section.RegionType == RegionTypeEnum.PartName)
            {
                _dctd.GetProperty("PartName").SetIsBrowsable(true);
                _dctd.GetProperty("ElementSetName").SetIsBrowsable(false);
            }
            else if (_section.RegionType == RegionTypeEnum.ElementSetName)
            {
                _dctd.GetProperty("PartName").SetIsBrowsable(false);
                _dctd.GetProperty("ElementSetName").SetIsBrowsable(true);
            }
        }
    }


}