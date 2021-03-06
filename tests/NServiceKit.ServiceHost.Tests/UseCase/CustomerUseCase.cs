using System;
using System.Data;
using System.Diagnostics;
using System.Linq.Expressions;
using Funq;
using NUnit.Framework;
using NServiceKit.CacheAccess;
using NServiceKit.CacheAccess.Providers;
using NServiceKit.Configuration;
using NServiceKit.OrmLite;
using NServiceKit.OrmLite.Sqlite;
using NServiceKit.ServiceHost.Tests.Support;
using NServiceKit.ServiceHost.Tests.TypeFactory;
using NServiceKit.ServiceHost.Tests.UseCase.Operations;
using NServiceKit.ServiceHost.Tests.UseCase.Services;

namespace NServiceKit.ServiceHost.Tests.UseCase
{
    /// <summary>A customer use case.</summary>
	[Ignore]
	[TestFixture]
	public class CustomerUseCase
	{
		private const int Times = 100000;

        /// <summary>Tests fixture set up.</summary>
		[TestFixtureSetUp]
		public void TestFixtureSetUp()
		{
			OrmLite.OrmLiteConfig.DialectProvider = new SqliteOrmLiteDialectProvider();
		}

        /// <summary>The use cache.</summary>
		public const bool UseCache = false;
		private ServiceController serviceController;

        /// <summary>Executes the before each test action.</summary>
		[SetUp]
		public void OnBeforeEachTest()
		{
			serviceController = new ServiceController(null);
		}

        /// <summary>Performance all IOC.</summary>
		[Test]
		public void Perf_All_IOC()
		{
			AutoWiredFunq_Perf();
			NativeFunq_Perf();
		}


        /// <summary>Native funq performance.</summary>
		[Test]
		public void NativeFunq_Perf()
		{
			RegisterServices(serviceController, GetNativeFunqTypeFactory());

			StoreAndGetCustomers(serviceController);

			var request = new GetCustomer { CustomerId = 2 };
			Console.WriteLine("NativeFunq_Perf(): {0}", Measure(() => serviceController.Execute(request), Times));
		}

        /// <summary>Automatic wired funq performance.</summary>
		[Test]
		public void AutoWiredFunq_Perf()
		{
			RegisterServices(serviceController, GetAutoWiredFunqTypeFactory());

			StoreAndGetCustomers(serviceController);

			var request = new GetCustomer { CustomerId = 2 };
			Console.WriteLine("AutoWiredFunq_Perf(): {0}", Measure(() => serviceController.Execute(request), Times));
		}

		private static long Measure(Action action, int iterations)
		{
			GC.Collect();
			var watch = Stopwatch.StartNew();

			for (int i = 0; i < iterations; i++)
			{
				action();
			}

			return watch.ElapsedTicks;
		}

        /// <summary>Using native funq.</summary>
		[Test]
		public void Using_NativeFunq()
		{
			RegisterServices(serviceController, GetNativeFunqTypeFactory());

			StoreAndGetCustomers(serviceController);
		}

        /// <summary>Using automatic wired funq.</summary>
		[Test]
		public void Using_AutoWiredFunq()
		{
			RegisterServices(serviceController, GetAutoWiredFunqTypeFactory());

			StoreAndGetCustomers(serviceController);
		}

		private static void StoreAndGetCustomers(ServiceController serviceController)
		{
			var storeCustomers = new StoreCustomers {
				Customers = {
	            	new Customer { Id = 1, FirstName = "First", LastName = "Customer" },
	            	new Customer { Id = 2, FirstName = "Second", LastName = "Customer" },
	            }
			};
			serviceController.Execute(storeCustomers);

			storeCustomers = new StoreCustomers {
				Customers = {
					new Customer {Id = 3, FirstName = "Third", LastName = "Customer"},
				}
			};
			serviceController.Execute(storeCustomers);

			var response = serviceController.Execute(new GetCustomer { CustomerId = 2 });

			Assert.That(response as GetCustomerResponse, Is.Not.Null);

			var customer = ((GetCustomerResponse)response).Customer;
			Assert.That(customer.FirstName, Is.EqualTo("Second"));
		}

		private static void RegisterServices(ServiceController serviceController, ITypeFactory typeFactory)
		{
			serviceController.RegisterGServiceExecutor(typeof(StoreCustomers), typeof(StoreCustomersService), typeFactory);
			serviceController.RegisterGServiceExecutor(typeof(GetCustomer), typeof(GetCustomerService), typeFactory);
		}

        /// <summary>Gets native funq type factory.</summary>
        ///
        /// <returns>The native funq type factory.</returns>
		public static ITypeFactory GetNativeFunqTypeFactory()
		{
			var container = GetContainerWithDependencies();

			container.Register(c => new StoreCustomersService(c.Resolve<IDbConnection>()))
				.ReusedWithin(ReuseScope.None);

			container.Register(c =>
					new GetCustomerService(c.Resolve<IDbConnection>(), c.Resolve<CustomerUseCaseConfig>()) {
						CacheClient = c.TryResolve<ICacheClient>()
					}
				)
				.ReusedWithin(ReuseScope.None);

			return new FuncTypeFactory(container);
		}

        /// <summary>Gets automatic wired funq type factory.</summary>
        ///
        /// <returns>The automatic wired funq type factory.</returns>
		public static ITypeFactory GetAutoWiredFunqTypeFactory()
		{
			var container = GetContainerWithDependencies();

			container.RegisterAutoWiredType(typeof(StoreCustomersService), typeof(GetCustomerService));

			return new ContainerResolveCache(container);
		}

		private static Container GetContainerWithDependencies()
		{
			var container = new Container();

			container.Register(c => ":memory:".OpenDbConnection())
				.ReusedWithin(ReuseScope.Container);
			container.Register<ICacheClient>(c => new MemoryCacheClient())
				.ReusedWithin(ReuseScope.Container);
			container.Register(c => new CustomerUseCaseConfig())
				.ReusedWithin(ReuseScope.Container);

			return container;
		}
	}

}
