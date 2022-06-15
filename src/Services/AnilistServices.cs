﻿namespace Yumiko.Services
{
    using GraphQL;
    using GraphQL.Client.Http;
    using GraphQL.Client.Serializer.Newtonsoft;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public static class AnilistServices
    {
        private static readonly GraphQLHttpClient GraphQlClient = new("https://graphql.anilist.co", new NewtonsoftJsonSerializer());

        public static async Task<Media> GetAniListMedia(InteractionContext ctx, double timeoutGeneral, string busqueda, MediaType type)
        {
            string query = "query($busqueda : String){" +
            "   Page(perPage:5){" +
            "       media(type: " + type.GetName() + ", search: $busqueda){" +
            "           id," +
            "           title{" +
            "               romaji" +
            "           }," +
            "           coverImage{" +
            "               large" +
            "           }," +
            "           siteUrl," +
            "           description," +
            "           format," +
            "           chapters" +
            "           episodes" +
            "           status," +
            "           meanScore," +
            "           genres," +
            "           seasonYear," +
            "           startDate{" +
            "               year," +
            "               month," +
            "               day" +
            "           }," +
            "           endDate{" +
            "               year," +
            "               month," +
            "               day" +
            "           }," +
            "           genres," +
            "           tags{" +
            "               name," +
            "               isMediaSpoiler" +
            "           }," +
            "           synonyms," +
            "           studios{" +
            "               nodes{" +
            "                   name," +
            "                   siteUrl" +
            "               }" +
            "           }," +
            "           externalLinks{" +
            "               site," +
            "               url" +
            "           }," +
            "           isAdult" +
            "       }" +
            "   }" +
            "}";

            var request = new GraphQLRequest
            {
                Query = query,
                Variables = new
                {
                    busqueda,
                },
            };

            try
            {
                var data = await GraphQlClient.SendQueryAsync<dynamic>(request);
                if (data.Data != null)
                {
                    if (data.Data.Page.media != null && data.Data.Page.media.Count > 0)
                    {
                        int cont = 0;
                        List<AnimeShort> opc = new();
                        foreach (var animeP in data.Data.Page.media)
                        {
                            cont++;
                            string opcTitle = animeP.title.romaji;
                            string opcFormat = animeP.format;
                            string opcYear = animeP.seasonYear;
                            string desc = opcFormat;
                            if (!string.IsNullOrEmpty(opcYear))
                            {
                                desc += $" ({opcYear})";
                            }

                            opc.Add(new AnimeShort
                            {
                                Title = opcTitle,
                                Description = $"{desc}",
                            });
                        }

                        var elegido = await Common.GetElegidoAsync(ctx, timeoutGeneral, opc);
                        if (elegido > 0)
                        {
                            var datos = data.Data.Page.media[elegido - 1];
                            return DecodeMedia(datos);
                        }
                        else
                        {
                            return new()
                            {
                                Ok = false,
                                MsgError = $"Response timed out",
                            };
                        }
                    }
                }

                return new()
                {
                    Ok = false,
                    MsgError = $"{type.GetName().ToLower()} not found: `{busqueda}`",
                };
            }
            catch (Exception e)
            {
                var context = ctx;
                await Common.GrabarLogErrorAsync(context, $"Error in AnilistUtils - GetAnilistMedia, type: {type.GetName()}\nError: {e.Message}");
                return new()
                {
                    Ok = false,
                    MsgError = $"{e.Message}",
                };
            }
        }

        public static async Task<(string, bool)> GetAniListMediaTitleAndNsfwFromId(InteractionContext ctx, int id, MediaType type)
        {
            string query = "query($id : Int){" +
            "   Media(type: " + type.GetName() + ", id: $id){" +
            "       title{" +
            "           romaji" +
            "       }," +
            "       isAdult" +
            "   }" +
            "}";

            var request = new GraphQLRequest
            {
                Query = query,
                Variables = new
                {
                    id,
                },
            };

            try
            {
                var data = await GraphQlClient.SendQueryAsync<dynamic>(request);
                if (data.Data != null)
                {
                    if (data.Data.Media != null)
                    {
                        var datos = data.Data.Media;
                        string nombreMedia = datos.title.romaji;
                        string nsfw = datos.isAdult;
                        return (nombreMedia, bool.Parse(nsfw));
                    }
                }

                return (string.Empty, false);
            }
            catch (Exception e)
            {
                await Common.GrabarLogErrorAsync(ctx, $"Error in AnilistUtils - GetAniListMediaTitleFromId, type: {type.GetName()}\nError: {e.Message}");
                return (string.Empty, false);
            }
        }

        public static Media? DecodeMedia(dynamic datos)
        {
            if (datos != null)
            {
                string idStr = datos.id;
                string isadult = datos.isAdult;

                Media media = new();

                media.Ok = true;
                media.Id = int.Parse(idStr);
                media.IsAdult = bool.Parse(isadult);
                media.Descripcion = datos.description;
                media.Descripcion = Common.NormalizarDescription(Common.LimpiarTexto(media.Descripcion));
                if (media.Descripcion == string.Empty)
                {
                    media.Descripcion = "(Without description)";
                }

                media.Estado = datos.status;
                media.Episodios = datos.episodes;
                media.Chapters = datos.chapters;
                media.Formato = datos.format;
                media.Score = $"{datos.meanScore}/100";
                media.Generos = string.Empty;
                foreach (var genero in datos.genres)
                {
                    media.Generos += genero;
                    media.Generos += ", ";
                }

                if (media.Generos.Length >= 2)
                {
                    media.Generos = media.Generos.Remove(media.Generos.Length - 2);
                }

                media.Tags = string.Empty;
                foreach (var tag in datos.tags)
                {
                    if (tag.isMediaSpoiler == "false")
                    {
                        media.Tags += tag.name;
                    }
                    else
                    {
                        media.Tags += $"||{tag.name}||";
                    }

                    media.Tags += ", ";
                }

                if (media.Tags.Length >= 2)
                {
                    media.Tags = media.Tags.Remove(media.Tags.Length - 2);
                }

                media.Titulos = new();
                foreach (string title in datos.synonyms)
                {
                    media.Titulos.Add(title);
                }

                media.Estudios = string.Empty;
                var nodos = datos.studios.nodes;
                if (nodos.HasValues)
                {
                    foreach (var studio in datos.studios.nodes)
                    {
                        media.Estudios += $"[{studio.name}]({studio.siteUrl}), ";
                    }
                }

                if (media.Estudios.Length >= 2)
                {
                    media.Estudios = media.Estudios.Remove(media.Estudios.Length - 2);
                }

                media.LinksExternos = string.Empty;
                foreach (var external in datos.externalLinks)
                {
                    media.LinksExternos += $"[{external.site}]({external.url}), ";
                }

                if (media.LinksExternos.Length >= 2)
                {
                    media.LinksExternos = media.LinksExternos.Remove(media.LinksExternos.Length - 2);
                }

                if (datos.startDate.day != null)
                {
                    if (datos.endDate.day != null)
                    {
                        media.Fechas = $"{datos.startDate.day}/{datos.startDate.month}/{datos.startDate.year} al {datos.endDate.day}/{datos.endDate.month}/{datos.endDate.year}";
                    }
                    else
                    {
                        media.Fechas = $"Airing since {datos.startDate.day}/{datos.startDate.month}/{datos.startDate.year}";
                    }
                }
                else
                {
                    media.Fechas = $"ENo air date available";
                }

                media.TituloRomaji = datos.title.romaji;
                media.UrlAnilist = datos.siteUrl;
                media.CoverImage = datos.coverImage.large;

                return media;
            }
            else
            {
                return null;
            }
        }

        public static async Task<DiscordEmbedBuilder?> GetInfoMediaUser(InteractionContext ctx, int anilistId, int mediaId)
        {
            var context = ctx;
            var requestPers = new GraphQLRequest
            {
                Query =
                    @"query ($codigoal: Int, $codigome: Int) {
                        MediaList(userId: $codigoal, mediaId: $codigome){
                            status,
                            progress,
                            startedAt {
                                year,
                                month,
                                day
                            },
                            completedAt {
                                year,
                                month,
                                day
                            },
                            notes,
                            score,
                            repeat,
                            media {
                                episodes,
                                chapters
                            },
                            user {
                                name,
                                avatar {
                                    large
                                },
                                mediaListOptions {
                                    scoreFormat
                                }
                            }
                        }
                    }",
                Variables = new
                {
                    codigoal = anilistId,
                    codigome = mediaId,
                },
            };
            try
            {
                var data = await GraphQlClient.SendQueryAsync<dynamic>(requestPers);
                if (data.Data != null)
                {
                    dynamic datos = data.Data.MediaList;
                    string status = datos.status;
                    string progress = datos.progress;
                    string episodiosMedia = datos.media.episodes;
                    string chaptersMedia = datos.media.chapters;
                    string scorePers = datos.score;
                    string startedd = datos.startedAt.day;
                    string startedm = datos.startedAt.month;
                    string startedy = datos.startedAt.year;
                    string completedd = datos.completedAt.day;
                    string completedm = datos.completedAt.month;
                    string completedy = datos.completedAt.year;
                    string notas = datos.notes;
                    string rewatches = datos.repeat;
                    string scoreFormat = datos.user.mediaListOptions.scoreFormat;
                    string nameAl = datos.user.name;
                    string avatarAl = datos.user.avatar.large;

                    if (string.IsNullOrEmpty(notas))
                    {
                        notas = "(Without notes)";
                    }

                    var builderPers = new DiscordEmbedBuilder
                    {
                        Title = $"Stats: {nameAl}",
                        Description = Common.NormalizarDescription("**Notes**\n" + notas),
                        Color = Constants.YumikoColor,
                    }.WithThumbnail(avatarAl);

                    builderPers.AddField("Status", status, true);
                    if (!string.IsNullOrEmpty(progress))
                    {
                        string episodios = progress;
                        if (!string.IsNullOrEmpty(episodiosMedia))
                        {
                            episodios += $"/{episodiosMedia}";
                        }

                        if (!string.IsNullOrEmpty(chaptersMedia))
                        {
                            episodios += $"/{chaptersMedia}";
                        }

                        builderPers.AddField("Episodes", episodios, true);
                    }

                    string scoreMostrar = string.Empty;
                    if (!string.IsNullOrEmpty(scorePers) && !string.IsNullOrEmpty(scoreFormat) && scorePers != "0")
                    {
                        string scoreF = string.Empty;
                        switch (scoreFormat)
                        {
                            case "POINT_10":
                            case "POINT_10_DECIMAL":
                                scoreF = $"{scorePers}/10";
                                break;
                            case "POINT_100":
                                scoreF = $"{scorePers}/100";
                                break;
                            case "POINT_5":
                                int scoreS = int.Parse(scorePers);
                                for (int i = 0; i < scoreS; i++)
                                {
                                    scoreF += "★";
                                }

                                break;
                            case "POINT_3":
                                int score3 = int.Parse(scorePers);
                                switch (score3)
                                {
                                    case 1:
                                        scoreF = "🙁";
                                        break;
                                    case 2:
                                        scoreF = "😐";
                                        break;
                                    case 3:
                                        scoreF = "🙂";
                                        break;
                                }

                                break;
                        }

                        builderPers.AddField("Score", scoreF, true);
                    }
                    else
                    {
                        builderPers.AddField("Score", "Not assigned", true);
                    }

                    if (!string.IsNullOrEmpty(rewatches))
                    {
                        builderPers.AddField("Rewatches", $"{rewatches}", false);
                    }

                    if (!string.IsNullOrEmpty(startedd) && !string.IsNullOrEmpty(startedm) && !string.IsNullOrEmpty(startedy))
                    {
                        builderPers.AddField("Start date", $"{startedd}/{startedm}/{startedy}", true);
                    }

                    if (!string.IsNullOrEmpty(completedd) && !string.IsNullOrEmpty(completedm) && !string.IsNullOrEmpty(completedy))
                    {
                        builderPers.AddField("End date", $"{completedd}/{completedm}/{completedy}", true);
                    }

                    return builderPers;
                }
            }
            catch (Exception ex)
            {
                if (ex.Message != "The HTTP request failed with status code NotFound")
                {
                    await Common.GrabarLogErrorAsync(context, $"Error in GetPersMedia: {ex.Message}\n```{ex.StackTrace}```");
                }
            }

            return null;
        }
    }
}