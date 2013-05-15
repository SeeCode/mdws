using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using gov.va.medora.mdws.dto.vista.mgt;
using System.ServiceModel;
using System.ServiceModel.Web;
using gov.va.medora.mdo.domain.pool.connection;
using gov.va.medora.mdo;
using gov.va.medora.mdo.domain.pool;
using gov.va.medora.mdo.dao;
using gov.va.medora.mdo.dao.vista;
using System.ServiceModel.Activation;
using gov.va.medora.mdws.dto;
using System.IO;
using gov.va.medora.utils;

namespace gov.va.medora.mdws
{
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class CrudSvc : ICrudSvc
    {
        MySession _mySession;

        public CrudSvc()
        {
            if (ConnectionPools.getInstance().PoolSource != null)
            {
                return; // already set up the pools!
            }

            _mySession = new MySession();
            SiteTable sites = _mySession.SiteTable;
            IList<AbstractPoolSource> sources = new List<AbstractPoolSource>();
            ConnectionPoolsSource poolsSource = new ConnectionPoolsSource();
            poolsSource.CxnSources = new Dictionary<string, ConnectionPoolSource>();
            User user = _mySession.MdwsConfiguration.ApplicationProxy;
            AbstractCredentials creds = new VistaCredentials();
            creds.AccountName = user.UserName;
            creds.AccountPassword = user.Pwd;
            creds.AuthenticationSource = new DataSource(); // BSE
            creds.AuthenticationSource.SiteId = new SiteId(user.LogonSiteId.Id, user.LogonSiteId.Name);
            creds.AuthenticationToken = user.LogonSiteId.Id + "_" + user.Uid;
            creds.LocalUid = user.Uid;
            creds.FederatedUid = user.SSN.toString();
            creds.SubjectName = user.Name.getLastNameFirst();
            creds.SubjectPhone = user.Phone;
            creds.SecurityPhrase = _mySession.MdwsConfiguration.AllConfigs
                [conf.MdwsConfigConstants.MDWS_CONFIG_SECTION][conf.MdwsConfigConstants.SECURITY_PHRASE];

            foreach (DataSource source in sites.Sources)
            {
                if (!String.Equals(source.Protocol, "VISTA", StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }
                ConnectionPoolSource newSource = new ConnectionPoolSource()
                {
                    LoadStrategy = (LoadingStrategy)Enum.Parse(typeof(LoadingStrategy), 
                        _mySession.MdwsConfiguration.AllConfigs[conf.MdwsConfigConstants.CONNECTION_POOL_CONFIG_SECTION][conf.MdwsConfigConstants.CONNECTION_POOL_LOAD_STRATEGY]),
                    MaxPoolSize = Convert.ToInt32(_mySession.MdwsConfiguration.AllConfigs[conf.MdwsConfigConstants.CONNECTION_POOL_CONFIG_SECTION][conf.MdwsConfigConstants.CONNECTION_POOL_MAX_CXNS]),
                    MinPoolSize = Convert.ToInt32(_mySession.MdwsConfiguration.AllConfigs[conf.MdwsConfigConstants.CONNECTION_POOL_CONFIG_SECTION][conf.MdwsConfigConstants.CONNECTION_POOL_MIN_CXNS]),
                    PoolExpansionSize = Convert.ToInt32(_mySession.MdwsConfiguration.AllConfigs[conf.MdwsConfigConstants.CONNECTION_POOL_CONFIG_SECTION][conf.MdwsConfigConstants.CONNECTION_POOL_EXPAND_SIZE]),
                    WaitTime = TimeSpan.Parse(_mySession.MdwsConfiguration.AllConfigs[conf.MdwsConfigConstants.CONNECTION_POOL_CONFIG_SECTION][conf.MdwsConfigConstants.CONNECTION_POOL_WAIT_TIME]),
                    Timeout = TimeSpan.Parse(_mySession.MdwsConfiguration.AllConfigs[conf.MdwsConfigConstants.CONNECTION_POOL_CONFIG_SECTION][conf.MdwsConfigConstants.CONNECTION_POOL_CXN_TIMEOUT]),
                    CxnSource = source,
                    Credentials = creds
                };
                newSource.CxnSource.Protocol = "PVISTA";
                poolsSource.CxnSources.Add(source.SiteId.Id, newSource);
            }

            ConnectionPools pools = (ConnectionPools)AbstractResourcePoolFactory.getResourcePool(poolsSource);
        }

        public TextTO create(System.IO.Stream postBody)
        {
            MySession newSession = new MySession();
            VistaRecordTO deserializedBody = null;
            try
            {
                String body = "";
                using (StreamReader sr = new StreamReader(postBody))
                {
                    body = sr.ReadToEnd();
                }
                deserializedBody = JsonUtils.Deserialize<VistaRecordTO>(body);
            }
            catch (Exception exc)
            {
                return new TextTO() { fault = new FaultTO(exc) };
            }

            return (TextTO)QueryTemplate.getQuery(QueryType.STATELESS, deserializedBody.siteId)
                .execute(newSession, new Func<VistaRecordTO, TextTO>(new ToolsLib(newSession).create),
                new object[] { deserializedBody });
        }

        public VistaRecordTO readAll(String siteId, String file, String recordId)
        {
            return read(siteId, file, recordId, "*");
        }

        public VistaRecordTO read(String siteId, String file, String recordId, String fields)
        {
            MySession newSession = new MySession();
            return (VistaRecordTO)QueryTemplate.getQuery(QueryType.STATELESS, siteId)
                .execute(newSession, new Func<String, String, String, VistaRecordTO>(new ToolsLib(newSession).read),
                new object[] { recordId, fields, file });
        }

        public TextTO update(Stream postBody)
        {
            MySession newSession = new MySession();
            VistaRecordTO deserializedBody = null;
            try
            {
                String body = "";
                using (StreamReader sr = new StreamReader(postBody))
                {
                    body = sr.ReadToEnd();
                }
                deserializedBody = JsonUtils.Deserialize<VistaRecordTO>(body);
            }
            catch (Exception exc)
            {
                return new TextTO() { fault = new FaultTO(exc) };
            }

            return (TextTO)QueryTemplate.getQuery(QueryType.STATELESS, deserializedBody.siteId)
                .execute(newSession, new Func< VistaRecordTO, TextTO>(new ToolsLib(newSession).update),
                new object[] { deserializedBody });
        }

        public TextTO delete(String siteId, String file, String recordId)
        {
            MySession newSession = new MySession();
            return (TextTO)QueryTemplate.getQuery(QueryType.STATELESS, siteId)
                .execute(newSession, new Func<String, String, TextTO>(new ToolsLib(newSession).delete),
                new object[] { recordId, file });
        }
    }

    [ServiceContract(Namespace = "http://mdws.medora.va.gov/CrudSvc")]
    interface ICrudSvc
    {
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, UriTemplate = "{siteId}/{file}/{recordId}/{fields}")]
        VistaRecordTO read(string siteId, string file, string recordId, string fields);

        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, UriTemplate = "{siteId}/{file}/{recordId}")]
        VistaRecordTO readAll(string siteId, string file, string recordId);

        [OperationContract]
        [WebInvoke(Method = "POST", ResponseFormat = WebMessageFormat.Json, UriTemplate = "*")]
        TextTO create(System.IO.Stream postBody);

        [OperationContract]
        [WebInvoke(Method = "PUT", ResponseFormat = WebMessageFormat.Json, UriTemplate = "*")]
        TextTO update(System.IO.Stream postBody);

        [OperationContract]
        [WebInvoke(Method = "DELETE", ResponseFormat = WebMessageFormat.Json, UriTemplate = "{siteId}/{file}/{recordId}")]
        TextTO delete(string siteId, string file, string recordId);
    }
}