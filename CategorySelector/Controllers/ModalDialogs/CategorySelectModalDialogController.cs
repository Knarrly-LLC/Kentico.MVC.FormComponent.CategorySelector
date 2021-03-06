﻿using CMS.EventLog;
using CMS.Membership;
using CMS.SiteProvider;
using CMS.Taxonomy;
using Microsoft.Azure.Search;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using VisualAntidote.Kentico.MVC.FormComponent.CategorySelector.Models.ModalDialogs;
using VisualAntidote.Kentico.MVC.FormComponent.CategorySelector.Repository;

namespace VisualAntidote.Kentico.MVC.FormComponent.CategorySelector.Controllers.ModalDialogs
{
    [Authorize]
    public class VisualAntidoteCategorySelectModalDialogController : Controller
    {
        public ActionResult Index(List<string> IncludeSites, bool IncludeGlobalCategories = true, bool IncludeDisabledCategories = false, string CurrentCultureName = "en-US", int? MinimumSelectedCategoryNumber = null, int? MaximumSelectedCategoryNumber = null)
        {
            CategorySelectModalDialogViewModel model = new CategorySelectModalDialogViewModel(new List<CategorySelectItemViewModel>());

            bool userCanAccess = true;

            //Editors and above can see this data
            userCanAccess = MembershipContext.AuthenticatedUser.CheckPrivilegeLevel(CMS.Base.UserPrivilegeLevelEnum.Editor);


            if (userCanAccess)
            {
                try
                {
                    // Creates a CultureInfo object from the culture code
                    var culture = new CultureInfo(CurrentCultureName);

                    // Sets the current culture for the MVC application
                    Thread.CurrentThread.CurrentUICulture = culture;
                    Thread.CurrentThread.CurrentCulture = culture;


                    var categoriesInHeirarchy = _LoadCategories(IncludeSites, IncludeGlobalCategories, IncludeDisabledCategories);

                    model = new CategorySelectModalDialogViewModel(categoriesInHeirarchy);
                    model.MinimumSelectedCategoryNumber = MinimumSelectedCategoryNumber;
                    model.MaximumSelectedCategoryNumber = MaximumSelectedCategoryNumber;
                }
                catch (Exception ex)
                {
                    model.IsError = true;
                    model.ErrorMessage = "Error loading categories. Please check the Event Log for more details.";
                    EventLogProvider.LogException("VisualAntidoteCategorySelectModalDialogController", "Error", ex);
                }


                // Return PartialView instead of View or else you get this error: The model item passed into the dictionary is of type 'Models.ModalDialogs.ColorModalDialogViewModel', but this dictionary requires a model item of type 'MedioClinic.Models.PageViewModel'.
                // That is because the normal view uses the _Layout which assumes uses of the PageViewModel object
                // We don't need the full view here, just the stand-alone modal dialog
                return PartialView("ModalDialogs/VisualAntidoteCategorySelectModalDialog/_CategorySelectModalDialog", model);
            }
            else
            {
                return new HttpNotFoundResult();
            }
        }


        private List<CategorySelectItemViewModel> _BuildTreeAndGetRoots(List<CategorySelectItemViewModel> actualObjects)
        {
            Dictionary<int, CategorySelectItemViewModel> lookup = new Dictionary<int, CategorySelectItemViewModel>();
            actualObjects.ForEach(x => lookup.Add(x.ID, new CategorySelectItemViewModel { CategoryLevel = x.CategoryLevel, ID = x.ID, ParentID = x.ParentID, CodeName = x.CodeName, Description = x.Description, DisplayName = x.DisplayName, GUID = x.GUID }));
            foreach (var item in lookup.Values)
            {
                CategorySelectItemViewModel proposedParent;
                if (item.ParentID.HasValue && lookup.TryGetValue(item.ParentID.Value, out proposedParent))
                {
                    item.Parent = proposedParent;
                    proposedParent.Categories.Add(item);
                }
            }
            return lookup.Values.Where(x => x.Parent == null).ToList();
        }

        private List<CategorySelectItemViewModel> _LoadCategories(List<string> IncludeSites, bool IncludeGlobalCategories, bool IncludeDisabledCategories)
        {
            List<CategorySelectItemViewModel> categoriesInHeirarchy = new List<CategorySelectItemViewModel>();

            try
            {
                var categoriesQuery = CategoryRepository.GenerateCategoryQuery(IncludeSites, IncludeGlobalCategories, IncludeDisabledCategories);

                var categories = categoriesQuery.ToList()
                    .Select(x => new CategorySelectItemViewModel()
                    {
                        ID = x.CategoryID,
                        CodeName = x.CategoryName,
                        DisplayName = x.CategoryDisplayName,
                        Description = x.CategoryDescription,
                        GUID = x.CategoryGUID,
                        ParentID = x.CategoryParentID,
                        CategoryLevel = x.CategoryLevel
                    }
                );

                categoriesInHeirarchy = _BuildTreeAndGetRoots(categories.ToList());
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return categoriesInHeirarchy;
        }
    }
}
