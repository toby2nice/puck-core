using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.EntityFrameworkCore;
using puckweb.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using puck.core.Entities;

namespace puckweb
{
    public class Startup
    {
        public Startup(IConfiguration configuration,IHostEnvironment env)
        {
            Configuration = configuration;
            Env = env;
        }
        IHostEnvironment Env { get; }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<PuckContext>(options =>
                options.UseSqlServer(
                    Configuration.GetConnectionString("DefaultConnection")));
            services.AddDefaultIdentity<PuckUser>(options => { options.SignIn.RequireConfirmedAccount = true;})
                .AddRoles<PuckRole>()
                .AddEntityFrameworkStores<PuckContext>();
            services.AddMemoryCache();
            services.AddResponseCaching();
            services.AddControllersWithViews().AddApplicationPart(typeof(puck.core.Controllers.BaseController).Assembly).AddControllersAsServices();
            services.AddRazorPages();
            services.AddAuthentication(puck.core.Constants.Mvc.AuthenticationScheme).AddCookie(options=> {
                options.LoginPath = "/puck/admin/in";
                options.LogoutPath = "/puck/admin/out";
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            puck.core.State.PuckCache.Configure(Configuration, env, app.ApplicationServices);
            puck.core.Bootstrap.Ini();
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseResponseCaching();
            app.UseRouting();

            app.UseAuthentication();
            
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapAreaControllerRoute(
                    name:"puckarea",
                    areaName:"puck",
                    pattern: "puck/{controller=Home}/{action=Index}/{id?}"
                    );
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });
        }
    }
}