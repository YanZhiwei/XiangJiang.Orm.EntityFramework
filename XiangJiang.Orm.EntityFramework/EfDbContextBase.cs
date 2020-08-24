﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using XiangJiang.Core;
using XiangJiang.Orm.Abstractions;
using XiangJiang.Orm.EntityFramework.Helper;

namespace XiangJiang.Orm.EntityFramework
{
    /// <summary>
    ///     基于EF的DbContext
    /// </summary>
    /// <seealso cref="System.Data.Entity.DbContext" />
    public abstract class EfDbContextBase : DbContext, IDbContext
    {
        #region Constructors

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="dbConnection">dbConnection</param>
        protected EfDbContextBase(DbConnection dbConnection)
            : base(dbConnection, true)
        {
            Configuration.LazyLoadingEnabled = false; //将不会查询到从表的数据，只会执行一次查询,可以使用 Inculde 进行手动加载;
            Configuration.ProxyCreationEnabled = false;
            Configuration.AutoDetectChangesEnabled = false;
        }

        #endregion Constructors

        #region Fields

        /// <summary>
        ///     获取 是否开启事务提交
        /// </summary>
        public virtual bool TransactionEnabled => Database.CurrentTransaction != null;

        #endregion Fields

        #region Methods

        /// <summary>
        ///     显式开启数据上下文事务
        /// </summary>
        /// <param name="isolationLevel">指定连接的事务锁定行为</param>
        public void BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.Unspecified)
        {
            if (!TransactionEnabled) Database.BeginTransaction(isolationLevel);
        }

        /// <summary>
        ///     提交当前上下文的事务更改
        /// </summary>
        public void Commit()
        {
            if (!TransactionEnabled) return;
            try
            {
                Database.CurrentTransaction.Commit();
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.InnerException is SqlException sqlEx)
                    throw new OrmException(sqlEx.Message, sqlEx);

                throw;
            }
        }

        /// <summary>
        ///     创建记录
        /// </summary>
        /// <returns>操作是否成功</returns>
        /// <param name="entity">需要操作的实体类.</param>
        public bool Create<T>(T entity)
            where T : ModelBase
        {
            Checker.Begin().NotNull(entity, nameof(entity));
            bool result;
            try
            {
                Entry(entity).State = EntityState.Added;
                result = SaveChanges() > 0;
            }
            catch (DbEntityValidationException dbEx)
            {
                throw new OrmException(dbEx.GetFullErrorText(), dbEx);
            }

            return result;
        }

        /// <summary>
        ///     创建记录集合
        /// </summary>
        /// <returns>操作是否成功.</returns>
        /// <param name="entities">实体类集合.</param>
        public bool Create<T>(IEnumerable<T> entities)
            where T : ModelBase
        {
            Checker.Begin().NotNull(entities, nameof(entities));
            bool result;
            try
            {
                foreach (var entity in entities) Entry(entity).State = EntityState.Added;

                result = SaveChanges() > 0;
            }
            catch (DbEntityValidationException dbEx)
            {
                throw new OrmException(dbEx.GetFullErrorText(), dbEx);
            }

            return result;
        }

        /// <summary>
        ///     删除记录
        /// </summary>
        /// <returns>操作是否成功</returns>
        /// <param name="entity">需要操作的实体类.</param>
        public bool Delete<T>(T entity)
            where T : ModelBase
        {
            Checker.Begin().NotNull(entity, nameof(entity));
            bool result;
            try
            {
                Entry(entity).State = EntityState.Deleted;
                result = SaveChanges() > 0;
            }
            catch (DbEntityValidationException dbEx)
            {
                throw new OrmException(dbEx.GetFullErrorText(), dbEx);
            }

            return result;
        }

        /// <summary>
        ///     条件判断是否存在
        /// </summary>
        /// <returns>是否存在</returns>
        /// <param name="predicate">判断条件委托</param>
        public bool Exist<T>(Expression<Func<T, bool>> predicate = null)
            where T : ModelBase
        {
            return predicate == null ? Set<T>().Any() : Set<T>().Any(predicate);
        }

        /// <summary>
        ///     根据id获取记录
        /// </summary>
        /// <returns>记录</returns>
        /// <param name="id">id.</param>
        public T GetByKeyId<T>(object id)
            where T : ModelBase
        {
            Checker.Begin().NotNull(id, nameof(id));
            return Set<T>().Find(id);
        }

        /// <summary>
        ///     条件获取记录集合
        /// </summary>
        /// <returns>集合</returns>
        /// <param name="predicate">筛选条件.</param>
        public List<T> GetList<T>(Expression<Func<T, bool>> predicate = null)
            where T : ModelBase
        {
            IQueryable<T> query = Set<T>();

            if (predicate != null) query = query.Where(predicate);

            return query.ToList();
        }

        /// <summary>
        ///     条件获取记录第一条或者默认
        /// </summary>
        /// <returns>记录</returns>
        /// <param name="predicate">筛选条件.</param>
        public T GetFirstOrDefault<T>(Expression<Func<T, bool>> predicate = null)
            where T : ModelBase
        {
            IQueryable<T> query = Set<T>();

            if (predicate != null)
                return query.FirstOrDefault(predicate);
            return query.FirstOrDefault();
        }

        /// <summary>
        ///     条件查询
        /// </summary>
        /// <returns>IQueryable</returns>
        /// <param name="predicate">筛选条件.</param>
        public IQueryable<T> Query<T>(Expression<Func<T, bool>> predicate = null)
            where T : ModelBase
        {
            IQueryable<T> query = Set<T>();

            if (predicate != null) query = query.Where(predicate);

            return query;
        }

        /// <summary>
        ///     显式回滚事务，仅在显式开启事务后有用
        /// </summary>
        public void Rollback()
        {
            if (TransactionEnabled) Database.CurrentTransaction.Rollback();
        }

        /// <summary>
        ///     执行Sql 脚本查询
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <param name="parameters">参数</param>
        /// <returns>集合</returns>
        public IEnumerable<T> SqlQuery<T>(string sql, IDbDataParameter[] parameters)
        {
            Checker.Begin().NotNullOrEmpty(sql, nameof(sql));
            // ReSharper disable once CoVariantArrayConversion
            return Database.SqlQuery<T>(sql, parameters);
        }

        /// <summary>
        ///     根据记录
        /// </summary>
        /// <returns>操作是否成功.</returns>
        /// <param name="entity">实体类记录.</param>
        public bool Update<T>(T entity)
            where T : ModelBase
        {
            Checker.Begin().NotNull(entity, nameof(entity));
            bool result;
            try
            {
                var set = Set<T>();
                set.Attach(entity);
                Entry(entity).State = EntityState.Modified;
                result = SaveChanges() > 0;
            }
            catch (DbEntityValidationException dbEx)
            {
                throw new OrmException(dbEx.GetFullErrorText(), dbEx);
            }

            return result;
        }

        #endregion Methods
    }
}