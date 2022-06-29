﻿namespace Yumiko.Utils
{
    using DiscordBotsList.Api;
    using Google.Cloud.Firestore;
    using Google.Cloud.Firestore.V1;
    using GraphQL;
    using GraphQL.Client.Http;
    using GraphQL.Client.Serializer.Newtonsoft;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
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

        public static FirestoreDb GetFirestoreClient()
        {
            string path = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "res", "firebase.json");
            string jsonString = File.ReadAllText(path);

            if (jsonString != null)
            {
                var deserialized = JsonConvert.DeserializeObject(jsonString);
                if (deserialized != null)
                {
                    JObject? data = (JObject)deserialized;
                    var projectId = data.SelectToken("project_id")?.Value<string>();
                    if (projectId != null)
                    {
                        var builder = new FirestoreClientBuilder { JsonCredentials = jsonString };
                        return FirestoreDb.Create(projectId, builder.Build());
                    }
                }
            }

            throw new NullReferenceException("Something go wrong with firease.json");
        }

        public static int GetRandomNumber(int min, int max)
        {
            Random rnd = new();

            if (min <= 0 && max <= 0)
            {
                return 0;
            }

            if (min + 1 == max)
            {
                return (rnd.Next(100) < 50) ? min : max;
            }

            return rnd.Next(minValue: min, maxValue: max);
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

        public static async Task ChequearVotoTopGGAsync(InteractionContext ctx, string topggToken)
        {
            if (Program.TopggEnabled && !Program.Debug)
            {
                AuthDiscordBotListApi DblApi = new(ctx.Client.CurrentApplication.Id, topggToken);
                bool hasVoted = await DblApi.HasVoted(ctx.User.Id);

                if (!hasVoted)
                {
                    string url = $"https://top.gg/bot/{ctx.Client.CurrentUser.Id}/vote";
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                    {
                        Title = translations.vote_me_on_topgg,
                        Description = string.Format(translations.vote_me_on_topgg_desc, url),
                        Color = Constants.YumikoColor,
                        Footer = new()
                        {
                            Text = translations.message_will_not_be_triggered
                        }
                    }));
                }
            }
        }

        public static async Task UpdateStatsTopGGAsync(ulong applicationId, string topggToken)
        {
            AuthDiscordBotListApi DblApi = new(applicationId, topggToken);
            var totalGuilds = Program.DiscordShardedClient.GuildCount();
            var totalShards = Program.DiscordShardedClient.ShardCount();

            await DblApi.UpdateStats(guildCount: totalGuilds, shardCount: totalShards);
        }

        public static async Task<int> GetElegidoAsync(InteractionContext ctx, double timeoutGeneral, List<TitleDescription> opciones)
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
                    options.Add(new DiscordSelectComponentOption(opc.Title.NormalizeButton(), $"{i}", opc.Description));
                }
            });

            var dropdown = new DiscordSelectComponent(customId, translations.select_an_option, options);

            var embed = new DiscordEmbedBuilder
            {
                Color = Constants.YumikoColor,
                Title = translations.choose_an_option,
            };

            DiscordMessage elegirMsg = await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddComponents(dropdown).AddEmbed(embed));

            var msgElegirInter = await interactivity.WaitForSelectAsync(elegirMsg, ctx.User, customId, TimeSpan.FromSeconds(timeoutGeneral));

            if (!msgElegirInter.TimedOut)
            {
                var resultElegir = msgElegirInter.Result;
                return int.Parse(resultElegir.Values[0]);
            }

            return -1;
        }

        public static async Task<bool> GetYesNoInteractivityAsync(InteractionContext ctx, double timeoutGeneral, InteractivityExtension interactivity, string title, string description)
        {
            DiscordButtonComponent yesButton = new(ButtonStyle.Success, "true", translations.yes);
            DiscordButtonComponent noButton = new(ButtonStyle.Danger, "false", translations.no);

            var msgBuilder = new DiscordFollowupMessageBuilder().AddEmbed(new DiscordEmbedBuilder
            {
                Title = title,
                Description = description,
            });

            msgBuilder.AddComponents(yesButton, noButton);

            DiscordMessage chooseMsg = await ctx.FollowUpAsync(msgBuilder);
            var msgElegirInter = await interactivity.WaitForButtonAsync(chooseMsg, ctx.User, TimeSpan.FromSeconds(timeoutGeneral));
            await ctx.DeleteFollowupAsync(chooseMsg.Id);
            if (!msgElegirInter.TimedOut)
            {
                return bool.Parse(msgElegirInter.Result.Id);
            }
            else
            {
                return false;
            }
        }

        public static async Task<CharacterOld?> GetRandomCharacterAsync(InteractionContext ctx, int pag)
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

                    return new CharacterOld()
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
                _ = ex.Message switch
                {
                    _ => await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{translations.unknown_error}: {ex.Message}")),
                };
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
                _ = ex.Message switch
                {
                    _ => await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent($"{translations.unknown_error}: {ex.Message}")),
                };
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

        public static async Task<byte[]> MergeImage(string link1, string link2, int x, int y)
        {
            var client = new HttpClient();
            var bytes1 = await client.GetByteArrayAsync(link1);
            var bytes2 = await client.GetByteArrayAsync(link2);

            using var memoryStream = new MemoryStream();
            using Image<Rgba32> img1 = Image.Load<Rgba32>(bytes1); // load up source images
            using Image<Rgba32> img2 = Image.Load<Rgba32>(bytes2);

            using var outputImage = new Image<Rgba32>(x, y); // create output image of the correct dimensions

            img1.Mutate(o => o.Resize(new Size(x / 2, y)));
            img2.Mutate(o => o.Resize(new Size(x / 2, y)));

            // take the 2 source images and draw them onto the image
            outputImage.Mutate(o => o
                .DrawImage(img1, new Point(0, 0), 1f) // draw the first one top left
                .DrawImage(img2, new Point(x / 2, 0), 1f)); // draw the second next to it

            // This saves to the memoryStream with encoder
            outputImage.Save(memoryStream, new PngEncoder());
            memoryStream.Position = 0; // The position needs to be reset.

            // return byte[]
            return memoryStream.ToArray();
        }

        public static byte[] OverlapImage(byte[] image1, byte[] image2, int x, int y)
        {
            using var memoryStream = new MemoryStream();
            using var outputImage = new Image<Rgba32>(x, y);
            using Image<Rgba32> img1 = Image.Load<Rgba32>(image1);
            using Image<Rgba32> img2 = Image.Load<Rgba32>(image2);

            outputImage.Mutate(o => o
                .DrawImage(img1, new Point(0, 0), 1f)
                .DrawImage(img2, new Point(0, 0), 1f));

            outputImage.Save(memoryStream, new PngEncoder());
            memoryStream.Position = 0;

            return memoryStream.ToArray();
        }

        public static FileInfo? GetNewestFile(DirectoryInfo directory)
        {
            return directory.GetFiles()
                .Union(directory.GetDirectories().Select(d => GetNewestFile(d)))
                .OrderByDescending(f => (f == null ? DateTime.MinValue : f.LastWriteTime))
                .FirstOrDefault();
        }
    }
}
