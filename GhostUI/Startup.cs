using System.Net;
using GhostUI.Hubs;
using GhostUI.Models;
using VueCliMiddleware;
using GhostUI.Extensions;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.SpaServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace GhostUI
{
    public class Startup
    {
        private readonly string _spaSourcePath;
        private readonly string _corsPolicyName;

        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            _spaSourcePath = Configuration.GetValue<string>("SPA:SourcePath");
            _corsPolicyName = Configuration.GetValue<string>("CORS:PolicyName");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Custom healthcheck example (using nuget package .AddHealthChecksUI() to view results at {url}/healthchecks-ui)
            services.AddHealthChecksUI()
                .AddHealthChecks()
                .AddGCInfoCheck("GCInfo");

            // Add CORS
            services.AddCorsConfig(_corsPolicyName);

            // Register RazorPages/Controllers
            services.AddControllers();

            // Add Brotli/Gzip response compression (prod only)
            services.AddResponseCompressionConfig(Configuration);

            // Add SignalR
            services.AddSignalR();

            // IMPORTANT CONFIG CHANGE IN 3.0 - 'Async' suffix in action names get stripped by default - so, to access them by full name with 'Async' part - opt out of this feature'.
            services.AddMvc(options => options.SuppressAsyncSuffixInActionNames = false);

            // In production, the Vue files will be served from this directory
            services.AddSpaStaticFiles(configuration => configuration.RootPath = $"{_spaSourcePath}/dist");

            // Register the Swagger services (using OpenApi 3.0)
            services.AddOpenApiDocument(configure => configure.Title = $"{this.GetType().Namespace} API");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // If development, enable Hot Module Replacement
            // If production, enable Brotli/Gzip response compression & strict transport security headers
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                // There does not yet appear to be a finalized solution to replace this obselete framework.
                // app.UseWebpackDevMiddleware(new WebpackDevMiddlewareOptions
                // {
                //     HotModuleReplacement = true
                // });
            }
            else
            {
                app.UseResponseCompression();
                app.UseExceptionHandler("/Error");
                app.UseHttpsRedirection();
                app.UseHsts();
            }

            // Global exception handling
            app.UseExceptionHandler(builder =>
            {
                builder.Run(async context =>
                {
                    var error = context.Features.Get<IExceptionHandlerFeature>();
                    var exDetails = new ExceptionDetails((int)HttpStatusCode.InternalServerError, error?.Error.Message);

                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = exDetails.StatusCode;
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.Headers.Add("Application-Error", exDetails.Message);
                    context.Response.Headers.Add("Access-Control-Expose-Headers", "Application-Error");

                    await context.Response.WriteAsync(exDetails.ToString());
                });
            });

            app.UseCors(_corsPolicyName);
            app.UseStaticFiles();

            // Register the Swagger generator and the Swagger UI middlewares
            // NSwage.MsBuild + adding automation config in GhostUI.csproj makes this part of the build step (updates to API will be handled automatically)
            app.UseOpenApi();
            app.UseSwaggerUi3(settings =>
            {
                settings.Path = "/docs";
                settings.DocumentPath = "/docs/api-specification.json";
            });

            app.UseSpaStaticFiles();
            app.UseRouting();

            // Map controllers / SignalR hubs / HealthChecks
            // Configure VueCliMiddleware package to handle startup of Vue.js ClientApp front-end
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<UsersHub>("/hubs/users");

                endpoints.MapHealthChecks("/healthchecks-json", new HealthCheckOptions
                {
                    Predicate = _ => true,
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                });

                endpoints.MapHealthChecksUI();

                if (System.Diagnostics.Debugger.IsAttached)
                    endpoints.MapToVueCliProxy("{*path}", new SpaOptions { SourcePath = _spaSourcePath }, "serve", regex: "running at");
                else
                    endpoints.MapFallbackToFile("index.html");
            });
        }
    }
}
