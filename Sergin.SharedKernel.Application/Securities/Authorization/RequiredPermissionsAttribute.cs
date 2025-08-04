namespace Sergin.SharedKernel.Application.Securities.Authorization;

[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiredPermissionsAttribute : Attribute
{
    public RequiredPermissionsAttribute(string permission, params string[] permissions)
    {
        Permissionas = [permission, .. permissions];
    }

    public Permission[] Permissionas { get; private set; }
}
