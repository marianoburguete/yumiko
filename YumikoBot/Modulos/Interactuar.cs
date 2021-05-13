﻿using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace Discord_Bot.Modulos
{
    public class Interactuar : BaseCommandModule
    {
        private readonly FuncionesAuxiliares funciones = new FuncionesAuxiliares();

        [Command("say"), Aliases("s"), Description("Yumiko habla en el chat.")]
        public async Task Say(CommandContext ctx, [Description("Mensaje para replicar")][RemainingText] string mensaje = null)
        {
            if (String.IsNullOrEmpty(mensaje))
            {
                var interactivity = ctx.Client.GetInteractivity();
                var msgAnime = await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                {
                    Title = "Escribe un mensaje",
                    Description = "Ejemplo: Hola! Soy Yumiko",
                    Footer = funciones.GetFooter(ctx),
                    Color = funciones.GetColor(),
                });
                var msgAnimeInter = await interactivity.WaitForMessageAsync(xm => xm.Channel == ctx.Channel && xm.Author == ctx.User, TimeSpan.FromSeconds(Convert.ToDouble(ConfigurationManager.AppSettings["TimeoutGeneral"])));
                if (!msgAnimeInter.TimedOut)
                {
                    mensaje = msgAnimeInter.Result.Content;
                    if (funciones.ChequearPermisoYumiko(ctx, DSharpPlus.Permissions.ManageMessages))
                    {
                        if (msgAnime != null)
                            await msgAnime.DeleteAsync("Auto borrado de Yumiko");
                        if (msgAnimeInter.Result != null)
                            await msgAnimeInter.Result.DeleteAsync("Auto borrado de Yumiko");
                    }
                }
                else
                {
                    var msgError = await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                    {
                        Title = "Error",
                        Description = "Tiempo agotado esperando un mensaje",
                        Footer = funciones.GetFooter(ctx),
                        Color = DiscordColor.Red,
                    });
                    await Task.Delay(3000);
                    if (funciones.ChequearPermisoYumiko(ctx, DSharpPlus.Permissions.ManageMessages))
                    {
                        if (msgError != null)
                            await msgError.DeleteAsync("Auto borrado de Yumiko");
                        if (msgAnime != null)
                            await msgAnime.DeleteAsync("Auto borrado de Yumiko");
                    }
                    return;
                }
            }
            await ctx.RespondAsync(mensaje);
        }

        [Command("pregunta"), Aliases("p", "question", "siono"), Description("Responde con SI O NO.")]
        public async Task Sisonon(CommandContext ctx, [Description("La pregunta que le quieres hacer")][RemainingText]string pregunta)
        {
            Random rnd = new Random();
            int random = rnd.Next(2);
            switch (random)
            {
                case 0:
                    await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                    {
                        Footer = funciones.GetFooter(ctx),
                        Color = DiscordColor.Red,
                        Title = "¿SIS O NON?",
                        Description = "**Pregunta:** " + pregunta + "\n**Respuesta:** NO"
                    }).ConfigureAwait(false);
                    break;
                case 1:
                    await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                    {
                        Footer = funciones.GetFooter(ctx),
                        Color = DiscordColor.Green,
                        Title = "¿SI O NO?",
                        Description = "**Pregunta:** " + pregunta + "\n**Respuesta:** SI"
                    }).ConfigureAwait(false);
                    break;
            }
        }

        [Command("elegir"), Aliases("e"), Description("Elige entre varias opciones.")]
        public async Task Elegir(CommandContext ctx, [Description("La pregunta de la cuál se eligirá una respuesta")][RemainingText]string pregunta)
        {
            var interactivity = ctx.Client.GetInteractivity();
            DiscordMessage mensajeBot = await ctx.Channel.SendMessageAsync("Ingrese las opciones separadas por comas").ConfigureAwait(false);
            var msg = await interactivity.WaitForMessageAsync(xm => xm.Author == ctx.User, TimeSpan.FromSeconds(60));
            if (!msg.TimedOut)
            {
                List<string> opciones = new List<string>();
                string msgResponse = msg.Result.Content;
                opciones = msgResponse.Split(',').ToList();
                Random rnd = new Random();
                int random = rnd.Next(opciones.Count);
                string options = "**Opciones:**";
                foreach (string msj in opciones)
                {
                    options += "\n   - " + msj;
                }
                if (funciones.ChequearPermisoYumiko(ctx, DSharpPlus.Permissions.ManageMessages))
                {
                    await mensajeBot.DeleteAsync().ConfigureAwait(false);
                    await msg.Result.DeleteAsync().ConfigureAwait(false);
                }
                await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                {
                    Footer = funciones.GetFooter(ctx),
                    Color = funciones.GetColor(),
                    Title = "Pregunta",
                    Description = "** " + pregunta + "**\n\n" + options + "\n\n**Respuesta:** " + opciones[random]
                }).ConfigureAwait(false);
            }
            else
            {
                await ctx.RespondAsync("No escribiste las opciones onii-chan" + ctx.User.Mention);
            }
        }

        [Command("emote"), Aliases("emoji"), Description("Muestra un emote en grande."), RequireGuild]
        public async Task Emote(CommandContext ctx, [Description("El emote para agrandar")] DiscordEmoji emote = null)
        {
            if (emote != null)
            {
                await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                {
                    Footer = funciones.GetFooter(ctx),
                    Color = funciones.GetColor(),
                    Title = emote.Name,
                    ImageUrl = emote.Url
                });
            }
            else
            {
                DiscordMessage msgError = await ctx.RespondAsync("Debes pasar un emote").ConfigureAwait(false);
                if (funciones.ChequearPermisoYumiko(ctx, DSharpPlus.Permissions.ManageMessages))
                {
                    await Task.Delay(3000);
                    await msgError.DeleteAsync("Auto borrado de yumiko").ConfigureAwait(false);
                }
            }
        }

        [Command("avatar"), Description("Muestra el avatar de un usuario."), RequireGuild]
        public async Task Avatar(CommandContext ctx, DiscordMember usuario = null)
        {
            if (usuario == null)
            {
                usuario = await ctx.Guild.GetMemberAsync(ctx.User.Id);
            }
            await ctx.RespondAsync(embed: new DiscordEmbedBuilder
            {
                Title = $"Avatar de {usuario.Username}#{usuario.Discriminator}",
                ImageUrl = usuario.AvatarUrl,
                Footer = funciones.GetFooter(ctx),
                Color = funciones.GetColor(),
                Url = usuario.AvatarUrl
            }).ConfigureAwait(false);
        }
    }
}
