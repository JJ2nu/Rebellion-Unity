using System;
using UnityEngine;

/// <summary>
/// Stage 위에 표시되는 모달이 게임플레이 입력을 잠글 때 사용하는 소유권 기반 게이트다.
/// 각 모달은 자신이 받은 Lease만 해제하므로 다른 모달의 잠금을 잘못 풀지 않는다.
/// </summary>
public static class StageInputModalGate
{
    private static int leaseCount;

    public static bool IsBlocked => leaseCount > 0;

    public static event Action<bool> BlockedStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        // Domain Reload를 끈 Editor 실행에서도 이전 Play Mode의 Lease와 구독자를 넘기지 않는다.
        leaseCount = 0;
        BlockedStateChanged = null;
    }

    public static IDisposable Acquire()
    {
        leaseCount++;
        if (leaseCount == 1)
        {
            BlockedStateChanged?.Invoke(true);
        }

        return new InputBlockLease();
    }

    private static void Release()
    {
        if (leaseCount <= 0)
        {
            return;
        }

        leaseCount--;
        if (leaseCount == 0)
        {
            BlockedStateChanged?.Invoke(false);
        }
    }

    private sealed class InputBlockLease : IDisposable
    {
        private bool isDisposed;

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            Release();
        }
    }
}
