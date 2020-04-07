﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CaeModel;
using CaeGlobals;
using System.Windows.Forms;

namespace PrePoMax.Forms
{
    class FrmSection : UserControls.FrmPropertyListView, IFormBase
    {
        // Variables                                                                                                                
        private string[] _sectionNames;
        private string _sectionToEditName;
        private ViewSection _viewSection;
        private Controller _controller;


        // Properties                                                                                                               
        public Section Section
        {
            get { return _viewSection != null ? _viewSection.GetBase() : null; }
            set
            {
                if (value is SolidSection) _viewSection = new ViewSolidSection((SolidSection)value.DeepClone());
                else throw new NotImplementedException();
            }
        }


        // Constructors                                                                                                             
        public FrmSection(Controller controller)
        {
            InitializeComponent();
            //
            _controller = controller;
            _viewSection = null;
            //
            _selectedPropertyGridItemChangedEventActive = true;
        }
        private void InitializeComponent()
        {
            this.gbType.SuspendLayout();
            this.gbProperties.SuspendLayout();
            this.SuspendLayout();
            // 
            // FrmSection
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.ClientSize = new System.Drawing.Size(334, 461);
            this.Name = "FrmSection";
            this.Text = "Edit Section";
            this.gbType.ResumeLayout(false);
            this.gbProperties.ResumeLayout(false);
            this.ResumeLayout(false);
        }


        // Overrides                                                                                                                
        protected override void OnListViewTypeSelectedIndexChanged()
        {
            if (lvTypes.Enabled && lvTypes.SelectedItems != null && lvTypes.SelectedItems.Count > 0)
            {
                object itemTag = lvTypes.SelectedItems[0].Tag;
                if (itemTag is ViewError) 
                {
                    _viewSection = null;
                }
                else if (itemTag is ViewSection vs)
                {
                    _viewSection = vs;
                }
                else throw new NotImplementedException();
                //
                SetSelectItem();
                //
                ShowHideSelectionForm();
                //
                propertyGrid.SelectedObject = itemTag;
                propertyGrid.Select();
                //
                HighlightSection();
            }
        }
        protected override void OnPropertyGridPropertyValueChanged()
        {
            string property = propertyGrid.SelectedGridItem.PropertyDescriptor.Name;
            //
            if (property == nameof(_viewSection.RegionType))
            {
                ShowHideSelectionForm();
                //
                HighlightSection();
            }
            else if (property == nameof(_viewSection.PartName) || property == nameof(_viewSection.ElementSetName))
            {
                HighlightSection();
            }
            //
            base.OnPropertyGridPropertyValueChanged();
        }
        protected override void OnApply(bool onOkAddNew)
        {
            if (propertyGrid.SelectedObject is ViewError ve) throw new CaeGlobals.CaeException(ve.Message);
            //
            _viewSection = (ViewSection)propertyGrid.SelectedObject;
            //
            if (_viewSection == null) throw new CaeGlobals.CaeException("No section was selected.");
            //
            if (Section.RegionType == RegionTypeEnum.Selection && (Section.CreationIds == null || Section.CreationIds.Length == 0))
                throw new CaeException("The section must contain at least one item.");
            //
            if ((_sectionToEditName == null && _sectionNames.Contains(_viewSection.Name)) ||                // create
                (_viewSection.Name != _sectionToEditName && _sectionNames.Contains(_viewSection.Name)))     // edit
                throw new CaeGlobals.CaeException("The selected section name already exists.");            
            // Create
            if (_sectionToEditName == null)
            {
                _controller.AddSectionCommand(Section);
            }
            // Replace
            else if(_propertyItemChanged)
            {
                _controller.ReplaceSectionCommand(_sectionToEditName, Section);
            }
            // If all is successful close the ItemSetSelectionForm - except for OKAddNew
            if (!onOkAddNew) ItemSetDataEditor.SelectionForm.Hide();
        }
        protected override void OnHideOrClose()
        {
            // Close the ItemSetSelectionForm
            ItemSetDataEditor.SelectionForm.Hide();
            //
            base.OnHideOrClose();
        }
        protected override bool OnPrepareForm(string stepName, string sectionToEditName)
        {
            // To prevent clear of the selection
            _selectedPropertyGridItemChangedEventActive = false;
            // To prevent the call to frmMain.itemForm_VisibleChanged when minimized
            this.DialogResult = DialogResult.None;      
            this.btnOkAddNew.Visible = sectionToEditName == null;
            //
            _propertyItemChanged = false;
            _sectionNames = null;
            _sectionToEditName = null;
            _viewSection = null;
            lvTypes.Items.Clear();
            propertyGrid.SelectedObject = null;            
            //
            _sectionNames = _controller.GetSectionNames();
            _sectionToEditName = sectionToEditName;
            string[] materialNames = _controller.GetMaterialNames();
            string[] solidPartNames = _controller.GetModelPartNames<CaeMesh.FeElement3D>();
            string[] solidElementSetNames = _controller.GetUserElementSetNames<CaeMesh.FeElement3D>();
            //
            if (_sectionNames == null)
                throw new CaeGlobals.CaeException("The section names must be defined first.");
            //
            PopulateListOfSections(materialNames, solidPartNames, solidElementSetNames);
            // Create new section
            if (_sectionToEditName == null)
            {
                lvTypes.Enabled = true;
                _viewSection = null;
                // Choose one - this is not needed if the user chooses
                if (lvTypes.Items.Count == 1)
                {
                    lvTypes.Items[0].Selected = true;
                    // ??? The code in the previous line does not call OnListViewTypeSelectedIndexChanged ???
                    if (lvTypes.Items[0].Tag is ViewSection vs)
                    {
                        _viewSection = vs;                          // ??? repeate ???
                        propertyGrid.SelectedObject = _viewSection; // ??? repeate ???
                        propertyGrid.Select();                      // ??? repeate ???
                    }
                }
            }
            // Edit existing section
            else
            {
                Section = _controller.GetSection(_sectionToEditName); // to clone
                if (Section.CreationData != null) Section.RegionType = RegionTypeEnum.Selection;
                // Select the appropriate constraint in the list view - disable event SelectedIndexChanged
                _lvTypesSelectedIndexChangedEventActive = false;
                if (_viewSection is ViewSolidSection) lvTypes.Items[0].Selected = true;
                lvTypes.Enabled = false;
                _lvTypesSelectedIndexChangedEventActive = true;
                //
                if (_viewSection is ViewSolidSection vss)
                {
                    // Check for deleted entities
                    CheckMissingValueRef(ref materialNames, vss.MaterialName, s => { vss.MaterialName = s; });
                    if (vss.RegionType == RegionTypeEnum.Selection.ToFriendlyString()) { }
                    else if (vss.RegionType == RegionTypeEnum.PartName.ToFriendlyString())
                        CheckMissingValueRef(ref solidPartNames, vss.PartName, s => { vss.PartName = s; });
                    else if (vss.RegionType == RegionTypeEnum.ElementSetName.ToFriendlyString())
                        CheckMissingValueRef(ref solidElementSetNames, vss.ElementSetName, s => { vss.ElementSetName = s; });
                    else throw new NotSupportedException();
                    //
                    _viewSection.PopululateDropDownLists(materialNames, solidPartNames, solidElementSetNames);
                    //
                    _controller.SetSelectItemToPart();
                }
                else throw new NotSupportedException();
                //
                propertyGrid.SelectedObject = _viewSection;
                propertyGrid.Select();
            }
            _selectedPropertyGridItemChangedEventActive = true;
            //
            SetSelectItem();
            //
            ShowHideSelectionForm();
            //
            HighlightSection(); // must be here if called from the menu
            //
            return true;
        }


        // Methods                                                                                                                  
        private void PopulateListOfSections(string[] materialNames, string[] partNames, string[] elementSetNames)
        {
            ListViewItem item;
            // Solid section
            item = new ListViewItem("Solid section");
            if (materialNames.Length > 0)
            {
                SolidSection ss = new SolidSection(GetSectionName(), materialNames[0], "", RegionTypeEnum.Selection);
                ViewSolidSection vss = new ViewSolidSection(ss);
                vss.PopululateDropDownLists(materialNames, partNames, elementSetNames);
                item.Tag = vss;
            }
            else item.Tag = new ViewError("There is no material defined for the solid section definition.");
            lvTypes.Items.Add(item);
        }
        private string GetSectionName()
        {
            return NamedClass.GetNewValueName(_sectionNames, "Section-");
        }
        private void HighlightSection()
        {
            try
            {
                _controller.ClearSelectionHistoryAndSelectionChanged();
                //
                if (_viewSection == null) { }
                else if (_viewSection is ViewSolidSection)
                {
                    if (Section.RegionType == RegionTypeEnum.PartName || Section.RegionType == RegionTypeEnum.ElementSetName)
                    {
                        _controller.Highlight3DObjects(new object[] { Section.RegionName });
                    }
                    else if (Section.RegionType == RegionTypeEnum.Selection)
                    {
                        SetSelectItem();
                        //
                        if (Section.CreationData != null)
                        {
                            _controller.Selection = Section.CreationData.DeepClone();
                            _controller.HighlightSelection();
                        }
                    }
                    else throw new NotSupportedException();
                }
                else throw new NotSupportedException();
            }
            catch { }
        }
        private void ShowHideSelectionForm()
        {
            if (Section != null && Section.RegionType == RegionTypeEnum.Selection)
                ItemSetDataEditor.SelectionForm.ShowIfHidden(this.Owner);
            else
                ItemSetDataEditor.SelectionForm.Hide();
        }
        private void SetSelectItem()
        {
            if (Section is null) { }
            else if (Section is SolidSection) _controller.SetSelectItemToPart();
        }
        //
        public void SelectionChanged(int[] ids)
        {
            if (Section != null && Section.RegionType == RegionTypeEnum.Selection)
            {
                if (Section is SolidSection)
                {
                    Section.CreationIds = ids;
                    Section.CreationData = _controller.Selection.DeepClone();
                    //
                    propertyGrid.Refresh();
                    //
                    _propertyItemChanged = true;
                }
                else throw new NotSupportedException();
            }
        }

    }
}
