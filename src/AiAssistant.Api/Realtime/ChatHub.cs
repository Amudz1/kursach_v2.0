using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AiAssistant.Application.Chats;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AiAssistant.Api.Realtime;

[Authorize]
public sealed class ChatHub(ChatAccess access, IAssistantData data, ISender sender) : Hub
{
    internal static string Group(Guid userId, Guid chatId) => $"chat:{userId:N}:{chatId:N}";

    public async Task JoinChat(Guid chatId)
    {
        var userId = await ActiveUserAsync();
        if (await access.FindAsync(userId, chatId, false, Context.ConnectionAborted) is null)
            throw new HubException("Чат не найден.");

        var group = Group(userId, chatId);
        if (Context.Items.TryGetValue("active-chat", out var existing) && existing is string previous && previous != group)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previous, Context.ConnectionAborted);

        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        Context.Items["active-chat"] = group;
    }

    public async Task StopGeneration(Guid messageId)
    {
        var userId = await ActiveUserAsync();
        var result = await sender.Send(new StopGeneration(userId, messageId), Context.ConnectionAborted);
        if (!result.IsSuccess) throw new HubException(result.Error!.Message);
    }

    private async Task<Guid> ActiveUserAsync()
    {
        var userId = Guid.Parse(Context.User!.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var active = await data.AnyAsync(data.Query<User>().Where(user => user.Id == userId && !user.IsBlocked),
            Context.ConnectionAborted);
        return active ? userId : throw new HubException("Сессия недоступна.");
    }
}
