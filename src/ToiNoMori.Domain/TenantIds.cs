namespace ToiNoMori.Domain;

/// <summary>
/// Stage 6R の移行データとMVS-01組織許可表に割り当てる内部テナント。
/// 外部IdPの組織識別子をこの値として直接信用してはならない。
/// </summary>
public static class TenantIds
{
    public static readonly Guid Mvs01 = Guid.Parse("7b48e239-07ef-4b34-a1fb-7f4fc7ff1673");
}
