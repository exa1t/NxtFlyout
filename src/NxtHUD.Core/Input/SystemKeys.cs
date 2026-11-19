namespace NxtHUD.Core.Input;

public enum VolumeKey
{
    Up,
    Down,
    Mute,
}

public enum LockKey
{
    CapsLock,
    NumLock,
    ScrollLock,
}

/// <summary>Maps Windows virtual-key codes to the volume and lock keys the app reacts to.</summary>
public static class SystemKeyMap
{
    public const int VkCapsLock = 0x14;
    public const int VkNumLock = 0x90;
    public const int VkScrollLock = 0x91;
    public const int VkVolumeMute = 0xAD;
    public const int VkVolumeDown = 0xAE;
    public const int VkVolumeUp = 0xAF;

    public static VolumeKey? VolumeFromVirtualKey(int virtualKey)
    {
        return virtualKey switch
        {
            VkVolumeUp => VolumeKey.Up,
            VkVolumeDown => VolumeKey.Down,
            VkVolumeMute => VolumeKey.Mute,
            _ => null,
        };
    }

    public static LockKey? LockFromVirtualKey(int virtualKey)
    {
        return virtualKey switch
        {
            VkCapsLock => LockKey.CapsLock,
            VkNumLock => LockKey.NumLock,
            VkScrollLock => LockKey.ScrollLock,
            _ => null,
        };
    }

    public static int ToVirtualKey(LockKey key)
    {
        return key switch
        {
            LockKey.NumLock => VkNumLock,
            LockKey.ScrollLock => VkScrollLock,
            _ => VkCapsLock,
        };
    }
}
