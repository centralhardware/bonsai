﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bonsai.Areas.Admin.Logic;
using Bonsai.Areas.Admin.Logic.Tree;
using Bonsai.Areas.Admin.ViewModels.Common;
using Bonsai.Areas.Admin.ViewModels.Relations;
using Bonsai.Code.Services;
using Bonsai.Code.Services.Jobs;
using Bonsai.Code.Utils.Validation;
using Bonsai.Data;
using Bonsai.Data.Models;
using Bonsai.Localization;
using Impworks.Utils.Format;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Bonsai.Areas.Admin.Controllers;

/// <summary>
/// Controller for managing relations.
/// </summary>
[Route("admin/relations")]
public class RelationsController(RelationsManagerService relsSvc, PagesManagerService pagesSvc, AppDbContext db, IBackgroundJobService jobs)
    : AdminControllerBase
{
    protected override Type ListStateType => typeof(RelationsListRequestVM);

    /// <summary>
    /// Displays the list of relations.
    /// </summary>
    [HttpGet]
    [Route("")]
    public async Task<ActionResult> Index(RelationsListRequestVM request)
    {
        PersistListState(request);
        ViewBag.Data = await GetDataAsync(request);
        var rels = await relsSvc.GetRelationsAsync(request);
        return View(rels);
    }

    /// <summary>
    /// Dispays the editor form for a new relation.
    /// </summary>
    [HttpGet]
    [Route("create")]
    public async Task<ActionResult> Create()
    {
        return await ViewEditorFormAsync(new RelationEditorVM { Type = RelationType.Child });
    }

    /// <summary>
    /// Attempts to create a new page.
    /// </summary>
    [HttpPost]
    [Route("create")]
    public async Task<ActionResult> Create(RelationEditorVM vm)
    {
        if(!ModelState.IsValid)
            return await ViewEditorFormAsync(vm);

        try
        {
            await relsSvc.CreateAsync(vm, User);
            await db.SaveChangesAsync();
            await jobs.RunAsync(JobBuilder.For<TreeLayoutJob>().SupersedeAll());

            return RedirectToSuccess(Texts.Admin_Relations_CreatedMessage);
        }
        catch(ValidationException ex)
        {
            SetModelState(ex);
            return await ViewEditorFormAsync(vm);
        }
    }

    /// <summary>
    /// Displays the editor for an existing relation.
    /// </summary>
    [HttpGet]
    [Route("update")]
    public async Task<ActionResult> Update(Guid id)
    {
        var vm = await relsSvc.RequestUpdateAsync(id);
        return await ViewEditorFormAsync(vm);
    }

    /// <summary>
    /// Attempts to update the existing relation.
    /// </summary>
    [HttpPost]
    [Route("update")]
    public async Task<ActionResult> Update(RelationEditorVM vm)
    {
        if(!ModelState.IsValid)
            return await ViewEditorFormAsync(vm);

        try
        {
            await relsSvc.UpdateAsync(vm, User);
            await db.SaveChangesAsync();
            await jobs.RunAsync(JobBuilder.For<TreeLayoutJob>().SupersedeAll());

            return RedirectToSuccess(Texts.Admin_Relations_UpdatedMessage);
        }
        catch(ValidationException ex)
        {
            SetModelState(ex);
            return await ViewEditorFormAsync(vm);
        }
    }

    /// <summary>
    /// Displays the relation removal confirmation.
    /// </summary>
    [HttpGet]
    [Route("remove")]
    public async Task<ActionResult> Remove(Guid id)
    {
        ViewBag.Info = await relsSvc.RequestRemoveAsync(id, User);
        return View(new RemoveEntryRequestVM { Id = id });
    }

    /// <summary>
    /// Removes the relation.
    /// </summary>
    [HttpPost]
    [Route("remove")]
    public async Task<ActionResult> Remove(RemoveEntryRequestVM vm)
    {
        if (vm.RemoveCompletely)
            await relsSvc.RemoveCompletelyAsync(vm.Id, User);
        else
            await relsSvc.RemoveAsync(vm.Id, User);
            
        await db.SaveChangesAsync();
        await jobs.RunAsync(JobBuilder.For<TreeLayoutJob>().SupersedeAll());

        return RedirectToSuccess(Texts.Admin_Relations_RemovedMessage);
    }

    /// <summary>
    /// Returns editor properties for the selected relation type.
    /// </summary>
    [HttpGet]
    [Route("editorProps")]
    public async Task<ActionResult> EditorProperties(RelationType relType)
    {
        return Json(relsSvc.GetPropertiesForRelationType(relType));
    }

    #region Helpers

    /// <summary>
    /// Displays the editor.
    /// </summary>
    private async Task<ActionResult> ViewEditorFormAsync(RelationEditorVM vm)
    {
        if(vm.SourceIds == null)
            vm.SourceIds = Array.Empty<Guid>();

        var pageIds = new[] {vm.DestinationId, vm.EventId}.Concat(vm.SourceIds.Cast<Guid?>()).ToList();
        var pageLookup = await pagesSvc.FindPagesByIdsAsync(pageIds);

        ViewBag.Data = new RelationEditorDataVM
        {
            IsNew = vm.Id == Guid.Empty,
            SourceItems = GetPageLookup(vm.SourceIds),
            DestinationItem = GetPageLookup(vm.DestinationId ?? Guid.Empty),
            EventItem = GetPageLookup(vm.EventId ?? Guid.Empty),

            Properties = relsSvc.GetPropertiesForRelationType(vm.Type),
            RelationTypes = EnumHelper.GetEnumValues<RelationType>()
                                      .Select(x => new SelectListItem
                                      {
                                          Value = x.ToString(),
                                          Text = x.GetLocaleEnumDescription(),
                                          Selected = x == vm.Type
                                      })
        };

        return View("Editor", vm);

        IReadOnlyList<SelectListItem> GetPageLookup(params Guid[] ids)
        {
            var result = new List<SelectListItem>();

            foreach(var id in ids)
                if(pageLookup.TryGetValue(id, out var page))
                    result.Add(new SelectListItem { Selected = true, Text = page.Title, Value = page.Id.ToString() });

            return result;
        }
    }

    /// <summary>
    /// Loads extra data for the filter.
    /// </summary>
    private async Task<RelationsListDataVM> GetDataAsync(RelationsListRequestVM request)
    {
        var data = new RelationsListDataVM();

        if (request.EntityId != null)
        {
            var title = await db.Pages
                                 .Where(x => x.Id == request.EntityId)
                                 .Select(x => x.Title)
                                 .FirstOrDefaultAsync();

            if (title != null)
                data.EntityTitle = title;
            else
                request.EntityId = null;
        }

        return data;
    }

    #endregion
}