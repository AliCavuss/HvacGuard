using System.ComponentModel.DataAnnotations;
using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;
using HvacGuard.Infrastructure.Common;
using HvacGuard.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HvacGuard.Web.Models;

public sealed class WorkOrderFormModel
{
    public int? Id { get; set; }

    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000), DataType(DataType.MultilineText)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select an air handling unit.")]
    [Display(Name = "Air handling unit")]
    public int UnitId { get; set; }

    [Display(Name = "Linked alarm")]
    public long? AlarmId { get; set; }

    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;

    [Display(Name = "Assigned to")]
    public string? AssignedTo { get; set; }

    [DataType(DataType.Date), Display(Name = "Due date")]
    public DateTime? DueDate { get; set; }

    [BindNever, ValidateNever]
    public IReadOnlyList<SelectListItem> UnitOptions { get; set; } = [];

    [BindNever, ValidateNever]
    public IReadOnlyList<SelectListItem> AssigneeOptions { get; set; } = [];

    [BindNever, ValidateNever]
    public string? AlarmMessage { get; set; }

    public WorkOrderInput ToInput() => new(Title, Description, UnitId, AlarmId, Priority, AssignedTo, DueDate);

    public static WorkOrderFormModel From(WorkOrder workOrder) => new()
    {
        Id = workOrder.Id,
        Title = workOrder.Title,
        Description = workOrder.Description,
        UnitId = workOrder.UnitId,
        AlarmId = workOrder.AlarmId,
        Priority = workOrder.Priority,
        AssignedTo = workOrder.AssignedTo,
        DueDate = workOrder.DueDate,
        AlarmMessage = workOrder.Alarm?.Message
    };
}

public sealed class WorkOrderListViewModel
{
    public required PagedResult<WorkOrder> Result { get; init; }
    public required WorkOrderQuery Query { get; init; }
    public IReadOnlyList<SelectListItem> VesselOptions { get; init; } = [];
    public IReadOnlyList<SelectListItem> AssigneeOptions { get; init; } = [];
}
