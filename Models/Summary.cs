using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
//using Reports.Migrations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Reports.Models
{
    public class Summary
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int PaymentVoucherId { get; set; }

        [ForeignKey(nameof(PaymentVoucherId))]
        [ValidateNever]
        public PaymentVoucher Voucher { get; set; }
        //public int VenueId { get; set; }
        //public virtual Venue? Venue { get; set; }
        public string Venue { get; set; }
        public int CountyId { get; set; }
        public virtual County? County { get; set; }
        public int HallHireNo { get; set; }
        public decimal EachHallAmount { get; set; }
        public int MpesaChargesPerHall { get; set; }
        public decimal HallHireTotal { get; set; }
        public decimal TotalMpesaChargesPerHall { get; set; }
        public int LeadFarmersNo { get; set; }
        public decimal EachLeadFarmersAmount { get; set; }
        public int MpesaChargesPerLeadFarmers { get; set; }
        public decimal TotalMpesaChargesPerLeadFarmers { get; set; }
        public decimal LeadFarmersTotal { get; set; }
        public int EANo { get; set; }
        public decimal EachEAAmount { get; set; }
        public int MpesaChargesPerEA { get; set; }
        public decimal TotalMpesaChargesPerEA { get; set; }
        public decimal EATotal { get; set; }
        public decimal SubTotalperVenue { get; set; }
        public decimal TotalSum { get; set; }
        [NotMapped]
        public DateTime ReportDate { get; internal set; }
        public PaymentStatus? status { get; set; }
        [NotMapped]
        public bool IsPaid
        {
            get => status == PaymentStatus.Paid;
            set => status = value ? PaymentStatus.Paid : PaymentStatus.Unpaid;
        }
    }

    public enum PaymentStatus
    {
        Paid = 0,
        Unpaid = 1
    } 
}
