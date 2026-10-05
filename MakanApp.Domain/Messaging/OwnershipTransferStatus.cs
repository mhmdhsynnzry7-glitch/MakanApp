namespace MakanApp.Domain.Messaging;

public enum OwnershipTransferStatus
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    Cancelled = 4,
    Expired = 5
}
