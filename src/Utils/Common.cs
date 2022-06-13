﻿namespace Yumiko.Utils
{
    using DiscordBotsList.Api;
    using DSharpPlus;
    using DSharpPlus.Entities;
    using DSharpPlus.Exceptions;
    using DSharpPlus.Interactivity;
    using DSharpPlus.Interactivity.Extensions;
    using DSharpPlus.SlashCommands;
    using Google.Cloud.Firestore;
    using GraphQL;
    using GraphQL.Client.Http;
    using GraphQL.Client.Serializer.Newtonsoft;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Formats.Png;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public static class Common
    {
        private static readonly GraphQLHttpClient GraphQlClient = new("https://graphql.anilist.co", new NewtonsoftJsonSerializer());

        public static FirestoreDb GetFirestoreClient(IConfiguration Configuration)
        {
            string path = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "res", @"firebase.json");
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);
            return FirestoreDb.Create(Configuration.GetValue<string>("firebase_database_name"));
        }

        public static int GetNumeroRandom(int min, int max)
        {
            if (min <= 0 && max <= 0)
            {
                return 0;
            }

            Random rnd = new();
            return rnd.Next(minValue: min, maxValue: max);
        }

        public static string NormalizarField(string s)
        {
            if (s.Length > 1024)
            {
                string aux = s.Remove(1024);
                int index = aux.LastIndexOf('[');
                if (index != -1)
                {
                    return aux.Remove(aux.LastIndexOf('[')) + "...";
                }
                else
                {
                    return aux.Remove(aux.Length - 4) + " ...";
                }
            }

            return s;
        }

        public static string NormalizarBoton(string s)
        {
            if (s.Length > 80)
            {
                return s.Remove(76) + " ...";
            }

            return s;
        }

        public static string NormalizarDescription(string s)
        {
            if (s.Length > 4096)
            {
                string aux = s.Remove(4096);
                int index = aux.LastIndexOf('[');
                if (index != -1)
                {
                    return aux.Remove(aux.LastIndexOf('[')) + "...";
                }
                else
                {
                    return aux.Remove(aux.Length - 4) + " ...";
                }
            }

            return s;
        }

        public static DiscordEmoji? ToEmoji(string text)
        {
            text = text.Trim();
            var match = Regex.Match(text, @"^<?a?:?([a-zA-Z0-9_]+):([0-9]+)>?$");
            if (!match.Success)
            {
                return DiscordEmoji.TryFromUnicode(text, out var emoji) ? emoji : null;
            }

            string json = $"{{\"name\":\"{match.Groups[1].Value}\", \"id\":{match.Groups[2].Value}," +
                $"\"animated\":{text.StartsWith("<a:").ToString().ToLower()}, \"require_colons\":true, \"available\":true}}";
            return JsonConvert.DeserializeObject<DiscordEmoji>(json);
        }

        public static string QuitarCaracteresEspeciales(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                return Regex.Replace(str, @"[^a-zA-Z0-9]+", " ").Trim();
            }

            return string.Empty;
        }

        public static string LimpiarTexto(string texto)
        {
            if (texto != null)
            {
                texto = texto.Replace("<br>", string.Empty);
                texto = texto.Replace("<Br>", string.Empty);
                texto = texto.Replace("<bR>", string.Empty);
                texto = texto.Replace("<BR>", string.Empty);
                texto = texto.Replace("<i>", "*");
                texto = texto.Replace("<I>", "*");
                texto = texto.Replace("</i>", "*");
                texto = texto.Replace("</I>", "*");
                texto = texto.Replace("~!", "||");
                texto = texto.Replace("!~", "||");
                texto = texto.Replace("__", "**");
                texto = texto.Replace("<b>", "**");
                texto = texto.Replace("<B>", "**");
                texto = texto.Replace("</b>", "**");
                texto = texto.Replace("</B>", "**");
            }
            else
            {
                texto = string.Empty;
            }

            return texto;
        }

        public static bool ChequearPermisoYumiko(InteractionContext ctx, Permissions permiso)
        {
            return PermissionMethods.HasPermission(ctx.Channel.PermissionsFor(ctx.Guild.CurrentMember), permiso);
        }

        public static async Task BorrarMensajeAsync(InteractionContext ctx, ulong msgId)
        {
            if (ChequearPermisoYumiko(ctx, Permissions.ManageMessages))
            {
                try
                {
                    var mensaje = await ctx.Channel.GetMessageAsync(msgId);
                    if (mensaje != null)
                    {
                        await mensaje.DeleteAsync("Yumiko auto delete");
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        public static async Task ChequearVotoTopGGAsync(InteractionContext ctx)
        {
            if (Program.TopggEnabled && !Program.Debug)
            {
                if (GetNumeroRandom(1, 10) == 1)
                {
                    /*
                    var json = string.Empty;
                    using (var fs = File.OpenRead("config.json"))
                    {
                        using var sr = new StreamReader(fs, new UTF8Encoding(false));
                        json = await sr.ReadToEndAsync();
                    }
                    var configJson = JsonConvert.DeserializeObject<ConfigJson>(json);

                    AuthDiscordBotListApi DblApi = new AuthDiscordBotListApi(ctx.Client.CurrentUser.Id, configJson.TopGG_token);
                    bool voto = await DblApi.HasVoted(ctx.User.Id);
                    */
                    bool voto = true;

                    if (voto)
                    {
                        string url = $"https://top.gg/bot/{ctx.Client.CurrentUser.Id}/vote";
                        var mensaje = await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                        {
                            Title = $"¡Votame en Top.gg!",
                            Description = $"Puedes ayudarme votando en [este sitio web]({url}). ¡Gracias!",
                            Color = Constants.YumikoColor,
                        }));
                        await Task.Delay(10000);
                        await BorrarMensajeAsync(ctx, mensaje.Id);
                    }
                }
            }
        }

        public static async Task UpdateStatsTopGGAsync(DiscordClient c, IConfiguration Configuration)
        {
            AuthDiscordBotListApi dblApi = new(c.CurrentUser.Id, Configuration.GetValue<string>("tokens:topgg"));
            await dblApi.UpdateStats(guildCount: c.Guilds.Count);
        }

        public static async Task<int> GetElegidoAsync(InteractionContext ctx, IConfiguration Configuration, List<AnimeShort> opciones)
        {
            int cantidadOpciones = opciones.Count;
            if (cantidadOpciones == 1)
            {
                return 1;
            }

            var interactivity = ctx.Client.GetInteractivity();
            List<DiscordSelectComponentOption> options = new();
            string customId = "dropdownGetElegido";

            int i = 0;
            opciones.ForEach(opc =>
            {
                if (i < 25 && opc.Title != null)
                {
                    i++;
                    options.Add(new DiscordSelectComponentOption(NormalizarBoton(opc.Title), $"{i}", opc.Description));
                }
            });

            var dropdown = new DiscordSelectComponent(customId, "Select an option", options);

            var embed = new DiscordEmbedBuilder
            {
                Color = Constants.YumikoColor,
                Title = "Choose an option",
            };

            DiscordMessage elegirMsg = await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddComponents(dropdown).AddEmbed(embed));

            var msgElegirInter = await interactivity.WaitForSelectAsync(elegirMsg, ctx.User, customId, TimeSpan.FromSeconds(Configuration.GetValue<double>("timeouts:general")));

            if (!msgElegirInter.TimedOut)
            {
                var resultElegir = msgElegirInter.Result;
                return int.Parse(resultElegir.Values[0]);
            }

            return -1;
        }

        public static async Task<bool> GetYesNoInteractivityAsync(InteractionContext ctx, IConfiguration Configuration, InteractivityExtension interactivity, string title, string description)
        {
            DiscordButtonComponent yesButton = new(ButtonStyle.Success, "true", "Yes");
            DiscordButtonComponent noButton = new(ButtonStyle.Danger, "false", "No");

            DiscordMessageBuilder msgBuilder = new()
            {
                Embed = new DiscordEmbedBuilder
                {
                    Title = title,
                    Description = description,
                },
            };

            msgBuilder.AddComponents(yesButton, noButton);

            DiscordMessage chooseMsg = await msgBuilder.SendAsync(ctx.Channel);
            var msgElegirInter = await interactivity.WaitForButtonAsync(chooseMsg, ctx.User, TimeSpan.FromSeconds(Configuration.GetValue<double>("timeouts:general")));
            await BorrarMensajeAsync(ctx, chooseMsg.Id);
            if (!msgElegirInter.TimedOut)
            {
                return bool.Parse(msgElegirInter.Result.Id);
            }
            else
            {
                return false;
            }
        }

        public static async Task<Character?> GetRandomCharacterAsync(InteractionContext ctx, int pag)
        {
            string titleMedia = string.Empty, siteUrlMedia = string.Empty;
            string query = "query($pagina: Int){" +
                        "   Page(page: $pagina, perPage: 1){" +
                        "       characters(sort: FAVOURITES_DESC){" +
                        "           name{" +
                        "               full" +
                        "           }," +
                        "           image{" +
                        "               large" +
                        "           }" +
                        "           siteUrl," +
                        "           favourites," +
                        "           media(sort: POPULARITY_DESC, perPage: 1){" +
                        "               nodes{" +
                        "                   title{" +
                        "                       romaji" +
                        "                   }," +
                        "                   siteUrl" +
                        "               }" +
                        "           }" +
                        "       }" +
                        "   }" +
                        "}";
            var request = new GraphQLRequest
            {
                Query = query,
                Variables = new
                {
                    pagina = pag,
                },
            };
            try
            {
                var data = await GraphQlClient.SendQueryAsync<dynamic>(request);
                foreach (var x in data.Data.Page.characters)
                {
                    string name = x.name.full;
                    string imageUrl = x.image.large;
                    string siteUrl = x.siteUrl;
                    int favoritos = x.favourites;
                    foreach (var m in x.media.nodes)
                    {
                        titleMedia = m.title.romaji;
                        siteUrlMedia = m.siteUrl;
                    }

                    return new Character()
                    {
                        NameFull = name,
                        Image = imageUrl,
                        SiteUrl = siteUrl,
                        Favoritos = favoritos,
                        AnimePrincipal = new Anime()
                        {
                            TitleRomaji = titleMedia,
                            SiteUrl = siteUrlMedia,
                        },
                    };
                }
            }
            catch (Exception ex)
            {
                await GrabarLogErrorAsync(ctx, $"Unknown error in GetRandomCharacter");
                DiscordMessage msg = ex.Message switch
                {
                    _ => await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"Unknown error: {ex.Message}")),
                };
                await Task.Delay(3000);
                await BorrarMensajeAsync(ctx, msg.Id);
                throw;
            }

            return null;
        }

        public static async Task<Anime?> GetRandomMediaAsync(InteractionContext ctx, int pag, MediaType type)
        {
            string query = "query($pagina: Int){" +
                        "   Page(page: $pagina, perPage: 1){" +
                        "       media(sort: FAVOURITES_DESC, isAdult: false, type:" + type.GetName() + "){" +
                        "           title{" +
                        "               romaji," +
                        "               english" +
                        "           }," +
                        "           coverImage{" +
                        "               large" +
                        "           }," +
                        "           siteUrl," +
                        "           favourites" +
                        "       }" +
                        "   }" +
                        "}";
            var request = new GraphQLRequest
            {
                Query = query,
                Variables = new
                {
                    pagina = pag,
                },
            };
            try
            {
                var data = await GraphQlClient.SendQueryAsync<dynamic>(request);
                foreach (var x in data.Data.Page.media)
                {
                    string titleRomaji = x.title.romaji;
                    string titleEnglish = x.title.english;
                    string imageUrl = x.coverImage.large;
                    string siteUrl = x.siteUrl;
                    int favoritos = x.favourites;
                    return new Anime()
                    {
                        TitleRomaji = titleRomaji,
                        TitleEnglish = titleEnglish,
                        Image = imageUrl,
                        SiteUrl = siteUrl,
                        Favoritos = favoritos,
                    };
                }
            }
            catch (Exception ex)
            {
                await GrabarLogErrorAsync(ctx, $"Unknown error in GetRandomMedia");
                DiscordMessage msg = ex.Message switch
                {
                    _ => await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"Unknown error: {ex.Message}")),
                };
                await Task.Delay(3000);
                await BorrarMensajeAsync(ctx, msg.Id);
                throw;
            }

            return null;
        }

        public static async Task GrabarLogErrorAsync(InteractionContext ctx, string descripcion)
        {
            await Program.LogChannelErrors.SendMessageAsync(new DiscordEmbedBuilder
            {
                Title = "Unknown error",
                Description = descripcion,
                Color = DiscordColor.Red,
                Author = new()
                {
                    IconUrl = ctx.Guild.IconUrl,
                    Name = ctx.Guild.Name,
                },
            }.AddField("Guild Id", $"{ctx.Guild.Id}", true)
            .AddField("Channel Id", $"{ctx.Channel.Id}", true)
            .AddField("Channel", $"#{ctx.Channel.Name}", false));
        }

        public static DiscordEmbedBuilder Waifu(DiscordMember miembro, bool real)
        {
            string nombre;
            string titulo = "Waifu";
            nombre = miembro.DisplayName;
            int waifuLevel;
            if (real)
            {
                Random rnd = new((int)miembro.Id);
                waifuLevel = rnd.Next(0, 100);
                titulo += " (REAL)";
            }
            else
            {
                waifuLevel = GetNumeroRandom(0, 100);
            }

            return waifuLevel switch
            {
                < 25 => new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Red,
                    Title = titulo,
                    Description = $"My love to {Formatter.Bold(nombre)} is {Formatter.Bold($"{waifuLevel}% ")}\n" +
                                    $"I will shot myself before touch you.",
                    ImageUrl = "https://i.imgur.com/BOxbruw.png",
                },
                < 50 => new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Orange,
                    Title = titulo,
                    Description = $"My love to {Formatter.Bold(nombre)} is {Formatter.Bold($"{waifuLevel}%")}\n" +
                                    $"You make me sick, I better get away from you.",
                    ImageUrl = "https://i.imgur.com/ys2HoiL.jpg",
                },
                < 75 => new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Yellow,
                    Title = titulo,
                    Description = $"My love to {Formatter.Bold(nombre)} is {Formatter.Bold($"{waifuLevel}%")}\n" +
                                    $"You're not bad, maybe you have a chance with me.",
                    ImageUrl = "https://i.imgur.com/h7Ic2rk.jpg",
                },
                < 100 => new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Green,
                    Title = titulo,
                    Description = $"My love to {Formatter.Bold(nombre)} is {Formatter.Bold($"{waifuLevel}%")}\n" +
                                    $"I am your waifu, you can do what you want with me.",
                    ImageUrl = "https://i.imgur.com/dhXR8mV.png",
                },
                _ => new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Blue,
                    Title = titulo,
                    Description = $"My love to {Formatter.Bold(nombre)} is {Formatter.Bold($"{waifuLevel}%")}\n" +
                                    $"I am completely in love with you, when do we get married?",
                    ImageUrl = "https://i.imgur.com/Vk6JMJi.jpg",
                },
            };
        }

        public static DiscordEmbedBuilder LogInteractionCommand(dynamic e, string titulo, bool parms, bool errored)
        {
            var builder = new DiscordEmbedBuilder()
            {
                Author = new()
                {
                    IconUrl = e.Context.Guild.IconUrl,
                    Name = e.Context.Guild.Name,
                },
                Title = titulo,
            }.AddField("Guild Id", $"{e.Context.Guild.Id}", true)
            .AddField("Channel Id", $"{e.Context.Channel.Id}", true)
            .AddField("Channel", $"#{e.Context.Channel.Name}", false);

            if (errored)
            {
                string desc = $"{e.Exception.Message}\n{Formatter.BlockCode(e.Exception.StackTrace)}";
                switch (e.Exception)
                {
                    case BadRequestException br:
                        dynamic? parsedJson = JsonConvert.DeserializeObject(br.Errors);
                        desc += $"\n{Formatter.BlockCode($"{br.JsonMessage}\n{JsonConvert.SerializeObject(parsedJson, Formatting.Indented)}")}";
                        break;
                    case NotFoundException nf:
                        desc += $"\n{Formatter.BlockCode(nf.JsonMessage)}";
                        break;
                    case RateLimitException rl:
                        desc += $"\n{Formatter.BlockCode(rl.JsonMessage)}";
                        break;
                    case RequestSizeException rz:
                        desc += $"\n{Formatter.BlockCode(rz.JsonMessage)}";
                        break;
                    case ServerErrorException se:
                        desc += $"\n{Formatter.BlockCode(se.JsonMessage)}";
                        break;
                    case UnauthorizedException ue:
                        desc += $"\n{Formatter.BlockCode(ue.JsonMessage)}";
                        break;
                }

                builder.WithDescription(desc);
                builder.WithColor(DiscordColor.Red);
            }
            else
            {
                builder.WithColor(DiscordColor.Green);
            }

            if (parms)
            {
                string options = string.Empty;
                var args = e.Context.Interaction.Data.Options;
                if (args != null)
                {
                    foreach (var arg in args)
                    {
                        options += $"{Formatter.InlineCode($"{arg.Name}: {arg.Value}")} ";
                    }
                }

                builder.AddField("Command", $"/{e.Context.CommandName} {options}", false);
            }
            else
            {
                builder.AddField("Command", $"/{e.Context.CommandName}", false);
            }

            return builder;
        }

        public static async Task<MemoryStream> MergeImage(string link1, string link2, int x, int y)
        {
            var client = new HttpClient();
            var bytes1 = await client.GetByteArrayAsync(link1);
            var bytes2 = await client.GetByteArrayAsync(link2);

            using var memoryStream = new MemoryStream();
            using Image<Rgba32> img1 = SixLabors.ImageSharp.Image.Load<Rgba32>(bytes1); // load up source images
            using Image<Rgba32> img2 = SixLabors.ImageSharp.Image.Load<Rgba32>(bytes2);

            using var outputImage = new Image<Rgba32>(x, y); // create output image of the correct dimensions

            img1.Mutate(o => o.Resize(new Size(x / 2, y)));
            img2.Mutate(o => o.Resize(new Size(x / 2, y)));

            // take the 2 source images and draw them onto the image
            outputImage.Mutate(o => o
                .DrawImage(img1, new Point(0, 0), 1f) // draw the first one top left
                .DrawImage(img2, new Point(x / 2, 0), 1f)); // draw the second next to it

            // Encode here for quality
            var encoder = new PngEncoder();

            // This saves to the memoryStream with encoder
            outputImage.Save(memoryStream, encoder);
            memoryStream.Position = 0; // The position needs to be reset.

            // prepare result to byte[]
            var myByteArray = memoryStream.ToArray();

            // return new stream
            return new MemoryStream(myByteArray)
            {
                Position = 0,
            };
        }
    }
}
