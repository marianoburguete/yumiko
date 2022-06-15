﻿namespace Yumiko
{
    using Microsoft.Extensions.Logging;

    public partial class Program
    {
        private static Task Client_Ready(DiscordClient sender, ReadyEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                await DiscordShardedClient.UpdateStatusAsync(new DiscordActivity { ActivityType = ActivityType.ListeningTo, Name = "/help" }, UserStatus.Online);
            });
            sender.Logger.LogInformation("DiscordClient ready to fire events");
            return Task.CompletedTask;
        }

        private static Task Client_Resumed(DiscordClient sender, ReadyEventArgs e)
        {
            sender.Logger.LogInformation("DiscordClient resumed");
            return Task.CompletedTask;
        }

        private static Task Client_GuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                var logGuildId = ConfigurationUtils.GetConfiguration<ulong>(Configuration, Configurations.LogginGuildId);
                var client = DiscordShardedClient.GetShard(logGuildId);

                if (client != null)
                {
                    var logGuild = await client.GetGuildAsync(logGuildId);

                    LogChannelApplicationCommands = logGuild.GetChannel(Debug ? ConfigurationUtils.GetConfiguration<ulong>(Configuration, Configurations.LogginTestingApplicationCommands) : ConfigurationUtils.GetConfiguration<ulong>(Configuration, Configurations.LogginProductionApplicationCommands));
                    LogChannelGuilds = logGuild.GetChannel(Debug ? ConfigurationUtils.GetConfiguration<ulong>(Configuration, Configurations.LogginTestingGuilds) : ConfigurationUtils.GetConfiguration<ulong>(Configuration, Configurations.LogginProductionGuilds));
                    LogChannelErrors = logGuild.GetChannel(Debug ? ConfigurationUtils.GetConfiguration<ulong>(Configuration, Configurations.LogginTestingErrors) : ConfigurationUtils.GetConfiguration<ulong>(Configuration, Configurations.LogginProductionErrors));

                    sender.Logger.LogInformation("Log guild and channels initialized", DateTime.Now);
                }
                else
                {
                    sender.Logger.LogCritical("Could not get loggin guild and channels");
                    await DiscordShardedClient.StopAsync();
                }
            });
            return Task.CompletedTask;
        }

        private static Task Client_GuildCreated(DiscordClient sender, GuildCreateEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                await LogChannelGuilds.SendMessageAsync(embed: new DiscordEmbedBuilder()
                {
                    Author = new()
                    {
                        IconUrl = e.Guild.IconUrl,
                        Name = $"{e.Guild.Name}",
                    },
                    Title = "New Guild",
                    Description =
                    $"   **Id**: {e.Guild.Id}\n" +
                    $"   **Members**: {e.Guild.MemberCount - 1}\n" +
                    $"   **Owner**: {e.Guild.Owner.Username}#{e.Guild.Owner.Discriminator}\n\n" +
                    $"   **Guild count**: {sender.Guilds.Count}",
                    Footer = new()
                    {
                        Text = $"{DateTimeOffset.Now}",
                    },
                    Color = DiscordColor.Green,
                });
                if (TopggEnabled && !Debug)
                {
                    await Common.UpdateStatsTopGGAsync(sender, ConfigurationUtils.GetConfiguration<string>(Configuration, Enums.Configurations.TokenTopgg));
                }
            });
            return Task.CompletedTask;
        }

        private static Task Client_GuildDeleted(DiscordClient sender, GuildDeleteEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                await LogChannelGuilds.SendMessageAsync(embed: new DiscordEmbedBuilder()
                {
                    Author = new()
                    {
                        IconUrl = e.Guild.IconUrl,
                        Name = $"{e.Guild.Name}",
                    },
                    Title = "Bye-bye guild",
                    Description =
                    $"   **Id**: {e.Guild.Id}\n" +
                    $"   **Members**: {e.Guild.MemberCount - 1}\n" +
                    $"   **Guild count**: {sender.Guilds.Count}",
                    Footer = new()
                    {
                        Text = $"{DateTimeOffset.Now}",
                    },
                    Color = DiscordColor.Red,
                });
                if (TopggEnabled && !Debug)
                {
                    await Common.UpdateStatsTopGGAsync(sender, ConfigurationUtils.GetConfiguration<string>(Configuration, Enums.Configurations.TokenTopgg));
                }
            });
            return Task.CompletedTask;
        }

        private static Task Client_ClientError(DiscordClient sender, ClientErrorEventArgs e)
        {
            sender.Logger.LogError("Client error. Check errors channel log");
            _ = Task.Run(async () =>
            {
                await LogChannelErrors.SendMessageAsync(embed: new DiscordEmbedBuilder()
                {
                    Title = "Client Error",
                    Description = $"{Formatter.BlockCode(e.Exception.StackTrace)}",
                    Footer = new()
                    {
                        Text = $"{DateTimeOffset.Now}",
                    },
                    Color = DiscordColor.Red,
                }.AddField("Type", $"{e.Exception.GetType()}", false)
                .AddField("Message", $"{e.Exception.Message}", false)
                .AddField("Event", $"{e.EventName}", false));
            });
            return Task.CompletedTask;
        }

        private static Task Client_ComponentInteractionCreated(DiscordClient sender, ComponentInteractionCreateEventArgs e)
        {
            if (e.Id.StartsWith("quiz-modal-"))
            {
                _ = Task.Run(async () =>
                {
                    var trivia = Singleton.GetInstance().GetCurrentTrivia(e.Guild.Id, e.Channel.Id);
                    if (trivia != null)
                    {
                        var btnInteraction = e.Interaction;
                        string modalId = $"quiz-modal-{btnInteraction.Id}";

                        var modal = new DiscordInteractionResponseBuilder()
                            .WithCustomId(modalId)
                            .WithTitle($"Guess the {trivia.Title}")
                            .AddComponents(new TextInputComponent(label: trivia.Title?.UppercaseFirst(), customId: "guess"));

                        await btnInteraction.CreateResponseAsync(InteractionResponseType.Modal, modal);
                    }
                });
            }
            else if (e.Id.StartsWith("quiz-cancel-"))
            {
                _ = Task.Run(async () =>
                {
                    var trivia = Singleton.GetInstance().GetCurrentTrivia(e.Guild.Id, e.Channel.Id);
                    if (trivia != null)
                    {
                        if (trivia.CreatedBy?.Id == e.User.Id)
                        {
                            trivia.Canceled = true;

                            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                            await e.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder()
                                .AsEphemeral(true)
                                .AddEmbed(new DiscordEmbedBuilder
                                {
                                    Title = $"You have canceled the game!",
                                    Color = DiscordColor.Red,
                                }));
                        }
                    }
                });
            }
            else
            {
                if (!e.Id.StartsWith("modal-"))
                {
                    _ = Task.Run(async () =>
                    {
                        await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                    });
                }
            }

            return Task.CompletedTask;
        }

        private static Task Client_ModalSubmitted(DiscordClient sender, ModalSubmitEventArgs e)
        {
            if (e.Interaction.Data.CustomId.StartsWith("quiz-modal-"))
            {
                _ = Task.Run(async () =>
                {
                    var modalInteraction = e.Interaction;
                    var trivia = Singleton.GetInstance().GetCurrentTrivia(e.Interaction.Guild.Id, e.Interaction.Channel.Id);
                    if (trivia != null)
                    {
                        var value = e.Values["guess"];
                        if (trivia.CurrentRound.Matches.Contains(value.ToLower()))
                        {
                            await modalInteraction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                                .AsEphemeral(true)
                                .AddEmbed(new DiscordEmbedBuilder
                                {
                                    Title = "You guessed it",
                                    Color = DiscordColor.Green,
                                }));

                            trivia.CurrentRound.Guessed = true;
                            trivia.CurrentRound.Guesser = e.Interaction.User;
                            trivia.CurrentRound.GuessTime = modalInteraction.CreationTimestamp;
                        }
                        else
                        {
                            await modalInteraction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                                .AsEphemeral(true)
                                .AddEmbed(new DiscordEmbedBuilder
                                {
                                    Title = "Wrong choice",
                                    Description = $"Your attempt: `{value}`",
                                    Color = DiscordColor.Red,
                                }));
                        }
                    }
                    else
                    {
                        await modalInteraction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                                .AsEphemeral(true)
                                .AddEmbed(new DiscordEmbedBuilder
                                {
                                    Title = "No trivia",
                                    Description = $"There is no trivia in this channel",
                                    Color = DiscordColor.Red,
                                }));
                    }
                });
            }

            return Task.CompletedTask;
        }

        private static Task SlashCommands_SlashCommandExecuted(SlashCommandsExtension sender, SlashCommandExecutedEventArgs e)
        {
            e.Handled = true;
            _ = Task.Run(async () =>
            {
                await LogChannelApplicationCommands.SendMessageAsync(LogUtils.LogSlashCommand(e));
            });
            return Task.CompletedTask;
        }

        private static Task SlashCommands_SlashCommandErrored(SlashCommandsExtension sender, SlashCommandErrorEventArgs e)
        {
            e.Handled = true;
            _ = Task.Run(async () =>
            {
                if (e.Exception is SlashExecutionChecksFailedException ex)
                {
                    await e.Context.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
                    foreach (SlashCheckBaseAttribute check in ex.FailedChecks)
                    {
                        switch (check)
                        {
                            case SlashRequireOwnerAttribute:
                                await e.Context.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                                {
                                    Title = $"Access denied",
                                    Description = $"Only the bot owner can execute this command",
                                    Color = DiscordColor.Red,
                                }));
                                break;
                            case SlashRequireBotPermissionsAttribute bp:
                                await e.Context.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                                {
                                    Title = $"Bot permission required",
                                    Description = $"{e.Context.Client.CurrentUser.Username} need the {Formatter.InlineCode($"{bp.Permissions}")} to execute this command",
                                    Color = DiscordColor.Red,
                                }));
                                break;
                            case SlashRequireUserPermissionsAttribute up:
                                await e.Context.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                                {
                                    Title = $"User Permission required",
                                    Description = $"You need the {Formatter.InlineCode($"{up.Permissions}")} permission to execute this command",
                                    Color = DiscordColor.Red,
                                }));
                                break;
                            case SlashRequirePermissionsAttribute ubp:
                                await e.Context.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                                {
                                    Title = $"Permission required",
                                    Description = $"You and {e.Context.Client.CurrentUser.Username} needs the {Formatter.InlineCode($"{ubp.Permissions}")} permission to execute this command",
                                    Color = DiscordColor.Red,
                                }));
                                break;
                            case SlashRequireGuildAttribute:
                                await e.Context.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                                {
                                    Title = $"Guild required",
                                    Description = $"You can only execute this command in a guild",
                                    Color = DiscordColor.Red,
                                }));
                                break;
                            case SlashRequireDirectMessageAttribute:
                                await e.Context.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                                {
                                    Title = $"DM required",
                                    Description = $"You can only execute this command in direct messages",
                                    Color = DiscordColor.Red,
                                }));
                                break;
                        }
                    }
                }
                else
                {
                    await LogChannelErrors.SendMessageAsync(LogUtils.LogSlashCommandError(e));

                    if (e.Exception.StackTrace != null && e.Exception.StackTrace.Contains("Trivia"))
                    {
                        Singleton.GetInstance().RemoveCurrentTrivia(e.Context.Guild.Id, e.Context.Channel.Id);
                    }
                }
            });
            return Task.CompletedTask;
        }
    }
}