using Unity.Services.Core;
using Unity.Services.Authentication;
using UnityEngine;
using System.Threading.Tasks;

public class UnityServicesInitializer : MonoBehaviour
{
    private async void Awake()
    {
        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Signed in to Unity Services as: " + AuthenticationService.Instance.PlayerId);
        }
    }
}
