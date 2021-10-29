using AnimalAdoption.Common.Domain;
using AnimalAdoption.Common.Logic;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AnimalAdoption.Web.Portal
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        private string _connectionString;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<Configuration>(Configuration);

            _connectionString = ""; //SET YOUR SQL DB CONNECTION STRING HERE

            services.AddDbContext<AnimalAdoptionContext>(options =>
            {
                options.UseSqlServer(_connectionString);
            });

            // Required so that Azure FrontDoor will work with authentication
            services.Configure<ForwardedHeadersOptions>(options =>
            {
              options.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
            });
            
            services.AddHttpContextAccessor();
            services.AddMemoryCache();
            services.AddTransient<AnimalService>();

            // Uncomment this section for Challenge 5
            //services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            //    .AddMicrosoftIdentityWebApp(Configuration.GetSection("AzureAd"));

            services.AddRazorPages()
                // Uncomment this section for Challenge 5
                //.AddMvcOptions(options => 
                //{
                //    var policy = new AuthorizationPolicyBuilder()
                //        .RequireAuthenticatedUser()
                //        .Build();
                //    options.Filters.Add(new AuthorizeFilter(policy));
                //})
                //.AddMicrosoftIdentityUI()
                ;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, AnimalAdoptionContext db)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHttpsRedirection();
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            db.Database.EnsureCreated();
            // Required so that Azure FrontDoor will work with authentication
            app.UseForwardedHeaders();
            app.UseStaticFiles();

            var failurePercentage = Configuration.GetValue<int?>("SimulatedFailureChance");
            if (failurePercentage != null)
            {
                var rand = new Random();
                app.Use(async (context, next) =>
                {
                    if (failurePercentage <= rand.Next(0, 100))
                    {
                        await next.Invoke();
                    }
                    else
                    {
                        throw new Exception($"A simulated failure occurred - there is a {failurePercentage}% chance of this occurring");
                    }
                });
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                app.Use(async (context, next) =>
                {
                    var url = context.Request.Path.Value;
                    if (!url.ToLowerInvariant().Contains("/missingenvironmentvariable"))
                    {
                // rewrite and continue processing
                context.Request.Path = "/missingenvironmentvariable";
                    }

                    await next();
                });
            }

            app.UseRouting();

            // Uncomment this section for Challenge 5
            //app.UseAuthentication();
            //app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
            });
        }
    }
}
