namespace Argon.Api.Grains.Persistence.States;

using Models;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject, Serializable, GenerateSerializer]
public sealed partial record UsersJoinedToChannel
{
    [DataMember(Order = 0), MemoryPackOrder(0), Id(0)]
    public List<UsersToServerRelationDto> Users { get; set; } = new();
}