using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RadRiftGame.Contracts.Commands;
using RadRiftGame.Contracts.Events;
using RadRiftGame.Contracts.ValueObjects;
using RadRiftGame.Domain;
using RadRiftGame.Domain.Aggregates;
using RadRiftGame.Domain.Projections;
using RadRiftGame.Domain.Services;
using RadRiftGame.Domain.Services.Db;
using RadRiftGame.Domain.Services.ReportService;
using RadRiftGame.Infrastructure;
using GameProcessService = RadRiftGame.Domain.Services.GameProcessService;

namespace RadRiftGame
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var bus = new FakeBus();
            var storage = new EventStore(bus);
            var rep = new Repository<GameRoom>(storage);
            var commands = new GameRoomCommandHandlers(rep);
            
            // Commands
            bus.RegisterHandler<CreateGameRoom>(commands.Handle);
            bus.RegisterHandler<JoinGameRoom>(commands.Handle);
            bus.RegisterHandler<DecreaseUserHealth>(commands.Handle);
            
            // Projections
            var eventsCountDetailedByOneMinuteProjection = new GameRoomsPlayersCountProjection();
            bus.RegisterHandler<UserJoinedGameRoom>(eventsCountDetailedByOneMinuteProjection.Handle);
            bus.RegisterHandler<GameRoomCreated>(eventsCountDetailedByOneMinuteProjection.Handle);

            var eventsCountDetailedByOneMinuteProjectionExtended = new GameRoomsWithTwoPlayersProjectionExtended();
            bus.RegisterHandler<UserJoinedGameRoom>(eventsCountDetailedByOneMinuteProjectionExtended.Handle);
            bus.RegisterHandler<GameRoomCreated>(eventsCountDetailedByOneMinuteProjectionExtended.Handle);

            // Dependencies
            services.AddSingleton<FakeBus>(bus);
            services.AddSingleton<IReadModelFacade>(new ReadModelFacade());
            services.AddSingleton<IRepository<GameRoom>>(rep);
            services.AddSingleton<IGameProcessService, GameProcessService>();//(new GameProcessService(services.));
            
            services.AddControllers();

            services.AddDbContext<GamesDbContext>(
                item => item.UseSqlServer(Configuration.GetConnectionString("myconn")));
            
            // ReportingSrv
            var dbContextOptionsBuilder = new DbContextOptionsBuilder();
            dbContextOptionsBuilder.UseSqlServer(Configuration.GetConnectionString("myconn"));
            var gamesDbContext = new GamesDbContext(dbContextOptionsBuilder.Options);
            var gameReportingSrb = new GameReportService( gamesDbContext);
            services.AddSingleton<IGameReportService>(gameReportingSrb);//(new GameProcessService(services.));

            // projections
            var gameRoomsStatusProjectionExtended = new GameRoomsStatusProjectionExtended(gameReportingSrb);
            bus.RegisterHandler<GameStopped>(gameRoomsStatusProjectionExtended.Handle);
            bus.RegisterHandler<GameRoomCreated>(gameRoomsStatusProjectionExtended.Handle);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                var context = serviceScope.ServiceProvider.GetRequiredService<GamesDbContext>();
                context.Database.Migrate();
            }
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}