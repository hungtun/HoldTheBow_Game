using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

public class LogoutManager : MonoBehaviour
{

	public float ackTimeoutSeconds = 3f;
	public string targetSceneName = "Login";
	private HubConnection connection;
	private int heroId;
	private bool isLoggingOut = false;

	public event Action<int> OnPlayerLoggedOut;
	public event Action OnLogoutSuccess;
	public event Action<string> OnLogoutError;

	public void Initialize(HubConnection hubConnection, int playerHeroId)
	{
		connection = hubConnection;
		heroId = playerHeroId;

		connection.On<int>("PlayerLoggedOut", OnRemotePlayerLoggedOut);
		connection.On<int>("LogoutAck", OnLogoutAck);
	}

	public void OnLogoutButtonClicked()
	{
		Logout();
	}

	public void ForceLogout()
	{
		Logout();
	}

	private void OnConfirmationResult(bool confirmed)
	{
		if (confirmed)
		{
			Logout();
		}
	}

	public async void Logout()
	{
		if (isLoggingOut || connection?.State != HubConnectionState.Connected)
		{
			return;
		}

		isLoggingOut = true;

		try
		{
			var logoutTask = connection.InvokeAsync("Logout", heroId);
			var timeoutTask = Task.Delay(TimeSpan.FromSeconds(ackTimeoutSeconds));
			var completed = await Task.WhenAny(logoutTask, timeoutTask);

			if (completed == timeoutTask)
			{
				throw new TimeoutException("Logout ACK timeout");
			}

			StartCoroutine(CallRestApiLogoutCoroutine());

			Session.JwtToken = null;
			Session.Heroes = null;
			Session.SelectedHeroId = 0;
			PlayerPrefs.DeleteKey("jwt_token");
			PlayerPrefs.DeleteKey("server_base_url");
			PlayerPrefs.Save();

			SceneManager.LoadScene(targetSceneName);
		}
		catch (Exception ex)
		{
			OnLogoutError?.Invoke(ex.Message);
			isLoggingOut = false;
		}
	}

	private void OnLogoutAck(int ackHeroId)
	{
		if (!isLoggingOut) return;

		isLoggingOut = false;

		OnLogoutSuccess?.Invoke();

	}

	private void OnRemotePlayerLoggedOut(int loggedOutHeroId)
	{
		OnPlayerLoggedOut?.Invoke(loggedOutHeroId);
	}

	private void OnApplicationPause(bool pauseStatus)
	{
		if (pauseStatus)
		{
			try { connection?.InvokeAsync("Logout", heroId); } catch { }
			try { connection?.StopAsync(); } catch { }
			try { connection?.DisposeAsync(); } catch { }
		}
	}

	private void OnApplicationQuit()
	{
		StartCoroutine(PerformCompleteLogoutCoroutine());
	}

	private IEnumerator PerformCompleteLogoutCoroutine()
	{
		if (connection != null)
		{
			yield return connection.InvokeAsync("Logout", heroId);
		}

		yield return StartCoroutine(CallRestApiLogoutCoroutine());

		Session.JwtToken = null;
		Session.SelectedHeroId = 0;

		if (connection != null)
		{
			yield return connection.StopAsync();
			yield return connection.DisposeAsync();
		}
	}

	private IEnumerator CallRestApiLogoutCoroutine()
	{
		if (string.IsNullOrEmpty(Session.JwtToken))
			yield break;

		using (var uwr = new UnityEngine.Networking.UnityWebRequest("{}/Authentication/logout", "POST"))
		{
			uwr.SetRequestHeader("Authorization", $"Bearer {Session.JwtToken}");
			uwr.SetRequestHeader("Content-Type", "application/json");

			yield return uwr.SendWebRequest();
		}
	}

	private void OnDestroy()
	{
		try
		{
			connection?.Remove("PlayerLoggedOut");
			connection?.Remove("LogoutAck");
		}
		catch {

		}
	}
}