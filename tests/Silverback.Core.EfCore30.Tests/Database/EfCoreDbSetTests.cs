﻿// Copyright (c) 2018-2019 Sergio Aquilini
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Silverback.Database;
using Silverback.Messaging.Publishing;
using Silverback.Tests.Core.EFCore30.TestTypes;
using Silverback.Tests.Core.EFCore30.TestTypes.Model;
using Xunit;

namespace Silverback.Tests.Core.EFCore30.Database
{
    public class EfCoreDbSetTests : IDisposable
    {
        private readonly TestDbContext _dbContext;
        private readonly EfCoreDbContext<TestDbContext> _efCoreDbContext;
        private readonly SqliteConnection _connection;

        public EfCoreDbSetTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var dbOptions = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(_connection)
                .Options;
            _dbContext = new TestDbContext(dbOptions, Substitute.For<IPublisher>());
            _dbContext.Database.EnsureCreated();
            _efCoreDbContext = new EfCoreDbContext<TestDbContext>(_dbContext);
        }

        [Fact]
        public void Add_SomeEntity_EntityIsAdded()
        {
            _efCoreDbContext.GetDbSet<Person>().Add(new Person());

            _dbContext.Persons.Local.Count.Should().Be(1);
            _dbContext.Entry(_dbContext.Persons.Local.First()).State.Should().Be(EntityState.Added);
        }

        [Fact]
        public void Remove_ExistingEntity_EntityIsRemoved()
        {
            _dbContext.Persons.Add(new Person());
            _dbContext.SaveChanges();

            _efCoreDbContext.GetDbSet<Person>().Remove(_dbContext.Persons.First());

            _dbContext.Entry(_dbContext.Persons.First()).State.Should().Be(EntityState.Deleted);
        }

        [Fact]
        public void RemoveRange_ExistingEntities_EntitiesAreRemoved()
        {
            _dbContext.Persons.Add(new Person());
            _dbContext.Persons.Add(new Person());
            _dbContext.SaveChanges();

            _efCoreDbContext.GetDbSet<Person>().RemoveRange(_dbContext.Persons.ToList());

            _dbContext.Entry(_dbContext.Persons.First()).State.Should().Be(EntityState.Deleted);
            _dbContext.Entry(_dbContext.Persons.Skip(1).First()).State.Should().Be(EntityState.Deleted);
        }

        [Fact]
        public void Find_ExistingKey_EntityIsReturned()
        {
            _dbContext.Persons.Add(new Person() { Name = "Sergio" });
            _dbContext.Persons.Add(new Person() { Name = "Mandy" });
            _dbContext.SaveChanges();

            var person = _efCoreDbContext.GetDbSet<Person>().Find(2);

            person.Name.Should().Be("Mandy");
        }

        [Fact]
        public async Task FindAsync_ExistingKey_EntityIsReturned()
        {
            _dbContext.Persons.Add(new Person() { Name = "Sergio" });
            _dbContext.Persons.Add(new Person() { Name = "Mandy" });
            _dbContext.SaveChanges();

            var person = await _efCoreDbContext.GetDbSet<Person>().FindAsync(2);

            person.Name.Should().Be("Mandy");
        }
        
        [Fact]
        public void AsQueryable_EfCoreQueryableIsReturned()
        {
            var queryable = _efCoreDbContext.GetDbSet<Person>().AsQueryable();

            queryable.Should().NotBeNull();
            queryable.Should().BeOfType<EfCoreQueryable<Person>>();
        }

        [Fact]
        public void GetLocalCache_LocalEntitiesAreReturned()
        {
            _dbContext.Persons.Add(new Person() { Name = "Sergio" });
            _dbContext.Persons.Add(new Person() { Name = "Mandy" });

            var local = _efCoreDbContext.GetDbSet<Person>().GetLocalCache();

            local.Should().NotBeNull();
            local.Count().Should().Be(2);
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}