using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AzureSqlMfaSample.Entities;

[Table("PTSyncForm", Schema = "gbpm")]
public class PTSyncForm
{
    [Key]
    public long Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserEmployeeId { get; set; }
    public string FormKind { get; set; }
    public int FormNo { get; set; }
    public string FormContent { get; set; }
    public byte FormAction { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public byte Flag { get; set; }
    public byte RetryCount { get; set; }
}
