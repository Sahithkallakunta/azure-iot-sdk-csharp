﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Common.Service.Auth;

namespace Microsoft.Azure.Devices.Provisioning.Service
{
    /// <summary>
    /// Device Provisioning Service Client.
    /// </summary>
    /// <remarks>
    /// The IoT Hub Device Provisioning Service is a helper service for IoT Hub that enables automatic device
    ///     provisioning to a specified IoT hub without requiring human intervention. You can use the Device Provisioning
    ///     Service to provision millions of devices in a secure and scalable manner.
    ///
    /// This C# SDK provides an API to help developers to create and maintain Enrollments on the IoT Hub Device
    ///     Provisioning Service, it translate the rest API in C# Objects and Methods.
    ///
    /// To use the this SDK, you must include the follow package on your application.
    /// <code>
    /// // Include the following using to use the Device Provisioning Service APIs.
    /// using Microsoft.Azure.Devices.Provisioning.Service;
    /// </code>
    ///
    /// The main APIs are exposed by the <see cref="ProvisioningServiceClient"/>, it contains the public Methods that the
    ///     application shall call to create and maintain the Enrollments. The Objects in the <b>configs</b> package shall
    ///     be filled and passed as parameters of the public API, for example, to create a new enrollment, the application
    ///     shall create the object <see cref="IndividualEnrollment"/> with the appropriate enrollment configurations, and call the
    ///     <see cref="CreateOrUpdateIndividualEnrollmentAsync(IndividualEnrollment)"/>.
    ///
    /// The IoT Hub Device Provisioning Service supports SQL queries too. The application can create a new query using
    ///     one of the queries factories, for instance <see cref="CreateIndividualEnrollmentQuery(QuerySpecification)"/>, passing
    ///     the <see cref="QuerySpecification"/>, with the SQL query. This factory returns a <see cref="Query"/> object, which is an
    ///     active iterator.
    ///
    /// This C# SDK can be represented in the follow diagram, the first layer are the public APIs the your application
    ///     shall use:
    ///
    /// <code>
    /// +===============+       +==========================================+                           +============+   +===+
    /// |    configs    |------>|         ProvisioningServiceClient        |                        +->|    Query   |   |   |
    /// +===============+       +==+=================+==================+==+                        |  +======+=====+   | e |
    ///                           /                  |                   \                          |         |         | x |
    ///                          /                   |                    \                         |         |         | c |
    /// +-----------------------+-----+  +-----------+------------+  +-----+---------------------+  |         |         | e |
    /// | IndividualEnrollmentManager |  | EnrollmentGroupManager |  | RegistrationStatusManager |  |         |         | p |
    /// +---------------+------+------+  +-----------+------+-----+  +-------------+-------+-----+  |         |         | t |
    ///                  \      \                    |       \                     |        \       |         |         | i |
    ///                   \      +----------------------------+------------------------------+------+         |         | o |
    ///                    \                         |                             |                          |         | n |
    ///  +--------+      +--+------------------------+-----------------------------+--------------------------+-----+   | s |
    ///  |  auth  |----->|                                     ContractApiHttp                                      |   |   |
    ///  +--------+      +-------------------------------------------+----------------------------------------------+   +===+
    ///                                                              |
    ///                                                              |
    ///                        +-------------------------------------+------------------------------------------+
    ///                        |                 com.microsoft.azure.sdk.iot.deps.transport.http                |
    ///                        +--------------------------------------------------------------------------------+
    /// </code>
    /// </remarks>
    /// <see cref="https://docs.microsoft.com/en-us/azure/iot-dps">Azure IoT Hub Device Provisioning Service</see>
    /// <see cref="https://docs.microsoft.com/en-us/azure/iot-dps/about-iot-dps">Provisioning devices with Azure IoT Hub Device Provisioning Service</see>
    public class ProvisioningServiceClient : IDisposable
    {
        private static IContractApiHttp _contractApiHttp;
        static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(100);

        /// <summary>
        /// Create a new instance of the <code>ProvisioningServiceClient</code> that exposes
        /// the API to the Device Provisioning Service.
        /// </summary>
        /// <remarks>
        /// The Device Provisioning Service Client is created based on a <b>Provisioning Connection string</b>.
        /// Once you create a Device Provisioning Service on Azure, you can get the connection string on the Azure portal.
        /// </remarks>
        /// <see cref="http://portal.azure.com/">Azure portal</see>
        ///
        /// <param name="connectionString">the <code>string</code> that cares the connection string of the Device Provisioning Service.</param>
        /// <returns>The <code>ProvisioningServiceClient</code> with the new instance of this object.</returns>
        /// <exception cref="ArgumentException">if the connectionString is <code>null</code> or empty.</exception>
        public static ProvisioningServiceClient CreateFromConnectionString(string connectionString)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_001: [The createFromConnectionString shall create a new instance of this class using the provided connectionString.] */
            return new ProvisioningServiceClient(connectionString);
        }

        /// <summary>
        /// PRIVATE CONSTRUCTOR
        /// </summary>
        /// <param name="connectionString">the <code>string</code> that contains the connection string for the Provisioning service.</param>
        /// <exception cref="ArgumentException">if the connectionString is <code>null</code>, empty, or invalid.</exception>
        private ProvisioningServiceClient(string connectionString)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_002: [The constructor shall throw ArgumentException if the provided connectionString is null or empty.] */
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("connectionString cannot be null or empty");
            }

            /* SRS_PROVISIONING_SERVICE_CLIENT_21_003: [The constructor shall throw ArgumentException if the ProvisioningConnectionString or one of the inner Managers failed to create a new instance.] */
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_004: [The constructor shall create a new instance of the ContractApiHttp class using the provided connectionString.] */
            ServiceConnectionString provisioningConnectionString = ServiceConnectionString.Parse(connectionString);
            _contractApiHttp = new ContractApiHttp(
                provisioningConnectionString.HttpsEndpoint,
                provisioningConnectionString,
                ExceptionHandlingHelper.GetDefaultErrorMapping(),
                DefaultOperationTimeout,
                client => { });
        }

        /// <summary>
        /// Dispose the Provisioning Service Client and its dependencies.
        /// </summary>
        public void Dispose()
        {
            if (_contractApiHttp != null)
            {
                _contractApiHttp.Dispose();
                _contractApiHttp = null;
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Create or update a individual Device Enrollment record.
        /// </summary>
        /// <remarks>
        /// This API creates a new individualEnrollment or update a existed one. All enrollments in the Device Provisioning Service
        ///     contains a unique identifier called registrationId. If this API is called for an individualEnrollment with a
        ///     registrationId that already exists, it will replace the existed individualEnrollment information by the new one.
        ///     On the other hand, if the registrationId does not exit, this API will create a new individualEnrollment.
        ///
        /// If the registrationId already exists, this method will update existed enrollments. Note that update the
        ///     individualEnrollment will not change the status of the device that was already registered using the old individualEnrollment.
        ///
        /// To use the Device Provisioning Service API, you must include the follow package on your application.
        /// <code>
        /// // Include the following using to use the Device Provisioning Service APIs.
        /// using Microsoft.Azure.Devices.Provisioning.Service;
        /// </code>
        /// </remarks>
        /// <example>
        /// The follow code will create a new individualEnrollment that will provisioning the registrationid-1 using TPM attestation.
        /// <code>
        /// // IndividualEnrollment information.
        /// private const string PROVISIONING_CONNECTION_STRING = "HostName=ContosoProvisioning.azure-devices-provisioning.net;" +
        ///                                                       "SharedAccessKeyName=contosoprovisioningserviceowner;" +
        ///                                                       "SharedAccessKey=0000000000000000000000000000000000000000000=";
        /// private const string TPM_ENDORSEMENT_KEY = "tpm-endorsement-key";
        /// private const string REGISTRATION_ID = "registrationid-1";
        ///
        /// static void Main(string[] args)
        /// {
        ///     RunSample().GetAwaiter().GetResult();
        /// }
        ///
        /// public static async Task RunSample()
        /// {
        ///     // *********************************** Create a Provisioning Service Client ************************************
        ///     ProvisioningServiceClient provisioningServiceClient =
        ///             ProvisioningServiceClient.CreateFromConnectionString(PROVISIONING_CONNECTION_STRING);
        ///
        ///     // ************************************ Create the individualEnrollment ****************************************
        ///     Console.WriteLine("\nCreate a new individualEnrollment...");
        ///     Attestation attestation = new TpmAttestation(TPM_ENDORSEMENT_KEY);
        ///     IndividualEnrollment individualEnrollment =
        ///         new IndividualEnrollment(
        ///             REGISTRATION_ID,
        ///             attestation);
        ///     individualEnrollment.ProvisioningStatus = ProvisioningStatus.Disabled;
        ///     IndividualEnrollment individualEnrollmentResult = await provisioningServiceClient.CreateOrUpdateIndividualEnrollmentAsync(individualEnrollment);
        ///     Console.WriteLine("\nIndividualEnrollment created with success...");
        /// }
        /// </code>
        ///
        /// The follow code will update the provisioningStatus of the previous individualEnrollment from <b>disabled</b> to <b>enabled</b>.
        /// <code>
        /// // IndividualEnrollment information.
        /// private const string PROVISIONING_CONNECTION_STRING = "HostName=ContosoProvisioning.azure-devices-provisioning.net;" +
        ///                                                              "SharedAccessKeyName=contosoprovisioningserviceowner;" +
        ///                                                              "SharedAccessKey=0000000000000000000000000000000000000000000=";
        /// private const string REGISTRATION_ID = "registrationid-1";
        ///
        /// static void Main(string[] args)
        /// {
        ///     RunSample().GetAwaiter().GetResult();
        /// }
        ///
        /// public static async Task RunSample()
        /// {
        ///     // *********************************** Create a Provisioning Service Client ************************************
        ///     ProvisioningServiceClient provisioningServiceClient =
        ///             ProvisioningServiceClient.CreateFromConnectionString(PROVISIONING_CONNECTION_STRING);
        ///
        ///     // ************************* Get the content of the previous individualEnrollment ******************************
        ///     Console.WriteLine("\nGet the content of the previous individualEnrollment...");
        ///     Attestation attestation = new TpmAttestation(TPM_ENDORSEMENT_KEY);
        ///     IndividualEnrollment individualEnrollment = await deviceProvisioningServiceClient.GetIndividualEnrollmentAsync(REGISTRATION_ID);
        ///     individualEnrollment.ProvisioningStatus = ProvisioningStatus.Enabled;
        ///     IndividualEnrollment individualEnrollmentResult = await provisioningServiceClient.CreateOrUpdateIndividualEnrollmentAsync(individualEnrollment);
        ///     Console.WriteLine("\nIndividualEnrollment updated with success...");
        /// }
        /// </code>
        /// </example>
        /// <see cref="https://docs.microsoft.com/en-us/azure/iot-dps/">Azure IoT Hub Device Provisioning Service</see>
        /// <see cref="https://docs.microsoft.com/en-us/rest/api/iot-dps/deviceenrollment">Device Enrollment</see>
        ///
        /// <param name="individualEnrollment">the <see cref="IndividualEnrollment"/> object that describes the individualEnrollment that will be created of updated. It cannot be <code>null</code>.</param>
        /// <returns>An <see cref="IndividualEnrollment"/> object with the result of the create or update requested.</returns>
        /// <exception cref="ArgumentException">if the provided parameter is not correct.</exception>
        /// <exception cref="ProvisioningServiceClientTransportException">if the SDK failed to send the request to the Device Provisioning Service.</exception>
        /// <exception cref="ProvisioningServiceClientException">if the Device Provisioning Service was not able to create or update the individualEnrollment.</exception>
        public Task<IndividualEnrollment>CreateOrUpdateIndividualEnrollmentAsync(IndividualEnrollment individualEnrollment)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_008: [The CreateOrUpdateIndividualEnrollmentAsync shall create a new Provisioning individualEnrollment by calling the CreateOrUpdateAsync in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.CreateOrUpdateAsync(_contractApiHttp, individualEnrollment, CancellationToken.None);
        }

        public Task<IndividualEnrollment> CreateOrUpdateIndividualEnrollmentAsync(IndividualEnrollment individualEnrollment, CancellationToken cancellationToken)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_008: [The CreateOrUpdateIndividualEnrollmentAsync shall create a new Provisioning individualEnrollment by calling the CreateOrUpdateAsync in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.CreateOrUpdateAsync(_contractApiHttp, individualEnrollment, cancellationToken);
        }

        /// <summary>
        /// Create, update or delete a set of individual Device Enrollments.
        /// </summary>
        /// <remarks>
        /// This API provide the means to do a single operation over multiple individualEnrollments. A valid operation
        ///     is determined by <see cref="BulkOperationMode"/>, and can be 'create', 'update', 'updateIfMatchETag', or 'delete'.
        /// </remarks>
        /// <see cref="https://docs.microsoft.com/en-us/azure/iot-dps/">Azure IoT Hub Device Provisioning Service</see>
        /// <see cref="https://docs.microsoft.com/en-us/rest/api/iot-dps/deviceenrollment">Device Enrollment</see>
        ///
        /// <param name="bulkOperationMode">the <see cref="BulkOperationMode"/> that defines the single operation to do over the individualEnrollments. It cannot be <code>null</code>.</param>
        /// <param name="individualEnrollments">the collection of <see cref="IndividualEnrollment"/> that contains the description of each individualEnrollment. It cannot be <code>null</code> or empty.</param>
        /// <returns>A <see cref="BulkEnrollmentOperationResult"/> object with the result of operation for each enrollment.</returns>
        /// <exception cref="ArgumentException">if the provided parameters are not correct.</exception>
        /// <exception cref="ProvisioningServiceClientTransportException">if the SDK failed to send the request to the Device Provisioning Service.</exception>
        /// <exception cref="ProvisioningServiceClientException">if the Device Provisioning Service was not able to execute the bulk operation.</exception>
        public Task<BulkEnrollmentOperationResult> RunBulkEnrollmentOperationAsync(
                BulkOperationMode bulkOperationMode, IEnumerable<IndividualEnrollment> individualEnrollments)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_009: [The RunBulkEnrollmentOperationAsync shall do a Provisioning operation over individualEnrollment by calling the BulkOperationAsync in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.BulkOperationAsync(_contractApiHttp, bulkOperationMode, individualEnrollments, CancellationToken.None);
        }

        public Task<BulkEnrollmentOperationResult> RunBulkEnrollmentOperationAsync(
                BulkOperationMode bulkOperationMode, IEnumerable<IndividualEnrollment> individualEnrollments, CancellationToken cancellationToken)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_009: [The RunBulkEnrollmentOperationAsync shall do a Provisioning operation over individualEnrollment by calling the BulkOperationAsync in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.BulkOperationAsync(_contractApiHttp, bulkOperationMode, individualEnrollments, cancellationToken);
        }

        /// <summary>
        /// Retrieve the individualEnrollment information.
        /// </summary>
        /// <remarks>
        /// This method will return the enrollment information for the provided registrationId. It will retrieve
        ///     the correspondent individualEnrollment from the Device Provisioning Service, and return it in the
        ///     <see cref="IndividualEnrollment"/> object.
        ///
        /// If the registrationId do not exists, this method will throw <see cref="ProvisioningServiceClientNotFoundException"/>.
        ///     for more exceptions that this method can throw, please see <see cref="ExceptionHandlingHelper"/>
        /// </remarks>
        /// <param name="registrationId">the <code>string} that identifies the individualEnrollment. It cannot be {@code null</code> or empty.</param>
        /// <returns>The <see cref="IndividualEnrollment"/> with the content of the individualEnrollment in the Provisioning Device Service.</returns>
        /// <exception cref="ArgumentException">if the provided parameter is not correct.</exception>
        /// <exception cref="ProvisioningServiceClientTransportException">if the SDK failed to send the request to the Device Provisioning Service.</exception>
        /// <exception cref="ProvisioningServiceClientException">if the Device Provisioning Service was not able to execute the bulk operation.</exception>
        public Task<IndividualEnrollment> GetIndividualEnrollmentAsync(string registrationId)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_010: [The GetIndividualEnrollmentAsync shall retrieve the individualEnrollment information for the provided registrationId by calling the GetAsync in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.GetAsync(_contractApiHttp, registrationId, CancellationToken.None);
        }

        public Task<IndividualEnrollment> GetIndividualEnrollmentAsync(string registrationId, CancellationToken cancellationToken)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_010: [The GetIndividualEnrollmentAsync shall retrieve the individualEnrollment information for the provided registrationId by calling the GetAsync in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.GetAsync(_contractApiHttp, registrationId, cancellationToken);
        }

        /// <summary>
        /// Delete the individualEnrollment information.
        /// </summary>
        /// <remarks>
        /// This method will remove the individualEnrollment from the Device Provisioning Service using the
        ///     provided <see cref="IndividualEnrollment"/> information. The Device Provisioning Service will care about the
        ///     registrationId and the eTag on the individualEnrollment. If you want to delete the individualEnrollment regardless the
        ///     eTag, you can set the <code>eTag="*"} into the individualEnrollment, or use the {@link #deleteIndividualEnrollment(string)</code>
        ///     passing only the registrationId.
        ///
        /// Note that delete the individualEnrollment will not remove the Device itself from the IotHub.
        ///
        /// If the registrationId does not exists or the eTag not matches, this method will throw <see cref="ProvisioningServiceClientNotFoundException"/>.
        ///     for more exceptions that this method can throw, please see <see cref="ExceptionHandlingHelper"/>
        /// </remarks>
        /// <param name="individualEnrollment">the <see cref="IndividualEnrollment"/> that identifies the individualEnrollment. It cannot be <code>null</code>.</param>
        /// <exception cref="ArgumentException">if the provided parameter is not correct.</exception>
        /// <exception cref="ProvisioningServiceClientTransportException">if the SDK failed to send the request to the Device Provisioning Service.</exception>
        /// <exception cref="ProvisioningServiceClientException">if the Device Provisioning Service was not able to execute the bulk operation.</exception>
        public Task DeleteIndividualEnrollmentAsync(IndividualEnrollment individualEnrollment)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_011: [The DeleteIndividualEnrollmentAsync shall delete the individualEnrollment for the provided individualEnrollment by calling the DeleteAsync in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.DeleteAsync(_contractApiHttp, individualEnrollment, CancellationToken.None);
        }

        public Task DeleteIndividualEnrollmentAsync(IndividualEnrollment individualEnrollment, CancellationToken cancellationToken)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_011: [The DeleteIndividualEnrollmentAsync shall delete the individualEnrollment for the provided individualEnrollment by calling the DeleteAsync in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.DeleteAsync(_contractApiHttp, individualEnrollment, cancellationToken);
        }

        /// <summary>
        /// Delete the individualEnrollment information.
        /// </summary>
        /// <remarks>
        /// This method will remove the individualEnrollment from the Device Provisioning Service using the
        ///     provided registrationId. It will delete the enrollment regardless the eTag. It means that this API
        ///     correspond to the <see cref="DeleteIndividualEnrollmentAsync(string, string)"/> with the <code>eTag="*"</code>.
        ///
        /// Note that delete the enrollment will not remove the Device itself from the IotHub.
        ///
        /// If the registrationId does not exists, this method will throw <see cref="ProvisioningServiceClientNotFoundException"/>.
        ///     for more exceptions that this method can throw, please see <see cref="ExceptionHandlingHelper"/>
        /// </remarks>
        /// <param name="registrationId">the <code>string} that identifies the individualEnrollment. It cannot be {@code null</code> or empty.</param>
        /// <exception cref="ArgumentException">if the provided registrationId is not correct.</exception>
        /// <exception cref="ProvisioningServiceClientTransportException">if the SDK failed to send the request to the Device Provisioning Service.</exception>
        /// <exception cref="ProvisioningServiceClientException">if the Device Provisioning Service was not able to execute the bulk operation.</exception>
        public Task DeleteIndividualEnrollmentAsync(string registrationId)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_012: [The DeleteIndividualEnrollmentAsync shall delete the individualEnrollment for the provided registrationId by calling the DeleteAsync in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.DeleteAsync(_contractApiHttp, registrationId, CancellationToken.None);
        }

        public Task DeleteIndividualEnrollmentAsync(string registrationId, CancellationToken cancellationToken)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_012: [The DeleteIndividualEnrollmentAsync shall delete the individualEnrollment for the provided registrationId by calling the DeleteAsync in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.DeleteAsync(_contractApiHttp, registrationId, cancellationToken);
        }

        /// <summary>
        /// Delete the individualEnrollment information.
        /// </summary>
        /// <remarks>
        /// This method will remove the individualEnrollment from the Device Provisioning Service using the
        ///     provided registrationId and eTag. If you want to delete the enrollment regardless the eTag, you can
        ///     use <see cref="DeleteIndividualEnrollmentAsync(string)"/> or you can pass the eTag as <code>null</code>, empty, or
        ///     <code>"*"</code>.
        ///
        /// Note that delete the enrollment will not remove the Device itself from the IotHub.
        ///
        /// If the registrationId does not exists or the eTag does not matches, this method will throw
        ///     <see cref="ProvisioningServiceClientNotFoundException"/>. For more exceptions that this method can throw, please see
        ///     <see cref="ExceptionHandlingHelper"/>
        /// </remarks>
        /// <param name="registrationId">the <code>string</code> that identifies the individualEnrollment. It cannot be <code>null</code> or empty.</param>
        /// <param name="eTag">the <code>string</code> with the IndividualEnrollment eTag. It can be <code>null</code> or empty.
        ///             The Device Provisioning Service will ignore it in all of these cases.</param>
        /// <exception cref="ArgumentException">if the provided registrationId is not correct.</exception>
        /// <exception cref="ProvisioningServiceClientTransportException">if the SDK failed to send the request to the Device Provisioning Service.</exception>
        /// <exception cref="ProvisioningServiceClientException">if the Device Provisioning Service was not able to execute the bulk operation.</exception>
        public Task DeleteIndividualEnrollmentAsync(string registrationId, string eTag)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_013: [The DeleteIndividualEnrollmentAsync shall delete the individualEnrollment for the provided registrationId and etag by calling the DeleteAsync in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.DeleteAsync(_contractApiHttp, registrationId, CancellationToken.None, eTag);
        }

        public Task DeleteIndividualEnrollmentAsync(string registrationId, string eTag, CancellationToken cancellationToken)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_013: [The DeleteIndividualEnrollmentAsync shall delete the individualEnrollment for the provided registrationId and etag by calling the DeleteAsync in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.DeleteAsync(_contractApiHttp, registrationId, cancellationToken, eTag);
        }

        /// <summary>
        /// Factory to create a individualEnrollment query.
        /// </summary>
        /// <remarks>
        /// This method will create a new individualEnrollment query for Device Provisioning Service and return it
        ///     as a <see cref="Query"/> iterator.
        ///
        /// The Device Provisioning Service expects a SQL query in the <see cref="QuerySpecification"/>, for instance
        ///     <code>"SELECT * FROM enrollments"</code>.
        /// </remarks>
        /// <param name="querySpecification">the <see cref="QuerySpecification"/> with the SQL query. It cannot be <code>null</code>.</param>
        /// <returns>The <see cref="Query"/> iterator.</returns>
        /// <exception cref="ArgumentException">if the provided parameter is not correct.</exception>
        public Query CreateIndividualEnrollmentQuery(QuerySpecification querySpecification)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_014: [The CreateIndividualEnrollmentQuery shall create a new individual enrolment query by calling the CreateQuery in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.CreateQuery(querySpecification);
        }

        /// <summary>
        /// Factory to create a individualEnrollment query.
        /// </summary>
        /// <remarks>
        /// This method will create a new individualEnrollment query for Device Provisioning Service and return it
        ///     as a <see cref="Query"/> iterator.
        ///
        /// The Device Provisioning Service expects a SQL query in the <see cref="QuerySpecification"/>, for instance
        ///     <code>"SELECT * FROM enrollments"</code>.
        ///
        /// For each iteration, the Query will return a List of objects correspondent to the query result. The maximum
        ///     number of items per iteration can be specified by the pageSize. It is optional, you can provide <b>0</b> for
        ///     default pageSize or use the API <see cref="CreateIndividualEnrollmentQuery(QuerySpecification)"/>.
        /// </remarks>
        /// <param name="querySpecification">the <see cref="QuerySpecification"/> with the SQL query. It cannot be <code>null</code>.</param>
        /// <param name="pageSize">the <code>int</code> with the maximum number of items per iteration. It can be 0 for default, but not negative.</param>
        /// <returns>The <see cref="Query"/> iterator.</returns>
        /// <exception cref="ArgumentException">if the provided parameters are not correct.</exception>
        public Query CreateIndividualEnrollmentQuery(QuerySpecification querySpecification, int pageSize)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_015: [The CreateIndividualEnrollmentQuery shall create a new individual enrolment query by calling the CreateQuery in the IndividualEnrollmentManager.] */
            return IndividualEnrollmentManager.CreateQuery(querySpecification, pageSize);
        }

        /// <summary>
        /// Create or update an enrollment group record.
        /// </summary>
        /// <remarks>
        /// This API creates a new enrollment group or update a existed one. All enrollment group in the Device
        ///     Provisioning Service contains a unique identifier called enrollmentGroupId. If this API is called
        ///     with an enrollmentGroupId that already exists, it will replace the existed enrollmentGroup information
        ///     by the new one. On the other hand, if the enrollmentGroupId does not exit, it will be created.
        ///
        /// To use the Device Provisioning Service API, you must include the follow package on your application.
        /// <code>
        /// // Include the following using to use the Device Provisioning Service APIs.
        /// using Microsoft.Azure.Devices.Provisioning.Service;
        /// </code>
        /// </remarks>
        /// <see cref="https://docs.microsoft.com/en-us/azure/iot-dps/">Azure IoT Hub Device Provisioning Service</see>
        /// <see cref="https://docs.microsoft.com/en-us/rest/api/iot-dps/deviceenrollmentgroup">Device Enrollment Group</see>
        ///
        /// <param name="enrollmentGroup">the <see cref="EnrollmentGroup"/> object that describes the individualEnrollment that will be created of updated.</param>
        /// <returns>An <see cref="EnrollmentGroup"/> object with the result of the create or update requested.</returns>
        /// <exception cref="ProvisioningServiceClientException">if the Provisioning was not able to create or update the enrollment.</exception>
        public Task<EnrollmentGroup> CreateOrUpdateEnrollmentGroupAsync(EnrollmentGroup enrollmentGroup)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_016: [The createOrUpdateEnrollmentGroup shall create a new Provisioning enrollmentGroup by calling the createOrUpdate in the EnrollmentGroupManager.] */
            return EnrollmentGroupManager.CreateOrUpdateAsync(_contractApiHttp, enrollmentGroup, CancellationToken.None);
        }

        /// <summary>
        /// Retrieve the enrollmentGroup information.
        /// </summary>
        /// <remarks>
        /// This method will return the enrollmentGroup information for the provided enrollmentGroupId. It will retrieve
        ///     the correspondent enrollmentGroup from the Device Provisioning Service, and return it in the
        ///     <see cref="EnrollmentGroup"/> object.
        ///
        /// If the enrollmentGroupId does not exists, this method will throw <see cref="ProvisioningServiceClientNotFoundException"/>.
        ///     for more exceptions that this method can throw, please see <see cref="ExceptionHandlingHelper"/>
        /// </remarks>
        /// <param name="enrollmentGroupId">the <code>string</code> that identifies the enrollmentGroup. It cannot be <code>null</code> or empty.</param>
        /// <returns>The <see cref="EnrollmentGroup"/> with the content of the enrollmentGroup in the Provisioning Device Service.</returns>
        /// <exception cref="ProvisioningServiceClientException">if the Provisioning Device Service was not able to retrieve the enrollmentGroup information for the provided enrollmentGroupId.</exception>
        public Task<EnrollmentGroup> GetEnrollmentGroupAsync(string enrollmentGroupId)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_017: [The getEnrollmentGroup shall retrieve the enrollmentGroup information for the provided enrollmentGroupId by calling the get in the EnrollmentGroupManager.] */
            return EnrollmentGroupManager.GetAsync(_contractApiHttp, enrollmentGroupId, CancellationToken.None);
        }

        /// <summary>
        /// Delete the enrollmentGroup information.
        /// </summary>
        /// <remarks>
        /// This method will remove the enrollmentGroup from the Device Provisioning Service using the
        ///     provided <see cref="EnrollmentGroup"/> information. The Device Provisioning Service will care about the
        ///     enrollmentGroupId and the eTag on the enrollmentGroup. If you want to delete the enrollment regardless the
        ///     eTag, you can set the <code>eTag="*"</code> into the enrollmentGroup, or use the <see cref="DeleteEnrollmentGroupAsync(string)"/>.
        ///     passing only the enrollmentGroupId.
        ///
        /// Note that delete the enrollmentGroup will not remove the Devices itself from the IotHub.
        ///
        /// If the enrollmentGroupId does not exists or the eTag does not matches, this method will throw
        ///     <see cref="ProvisioningServiceClientNotFoundException"/>. For more exceptions that this method can throw, please see
        ///     <see cref="ExceptionHandlingHelper"/>.
        /// </remarks>
        /// <param name="enrollmentGroup">the <see cref="EnrollmentGroup"/> that identifies the enrollmentGroup. It cannot be <code>null</code>.</param>
        /// <exception cref="ProvisioningServiceClientException">if the Provisioning Device Service was not able to delete the enrollmentGroup information for the provided enrollmentGroup.</exception>
        public Task DeleteEnrollmentGroupAsync(EnrollmentGroup enrollmentGroup)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_018: [The deleteEnrollmentGroup shall delete the enrollmentGroup for the provided enrollmentGroup by calling the delete in the EnrollmentGroupManager.] */
            return EnrollmentGroupManager.DeleteAsync(_contractApiHttp, enrollmentGroup, CancellationToken.None);
        }

        /// <summary>
        /// Delete the enrollmentGroup information.
        /// </summary>
        /// <remarks>
        /// This method will remove the enrollmentGroup from the Device Provisioning Service using the
        ///     provided enrollmentGroupId. It will delete the enrollmentGroup regardless the eTag. It means that this API
        ///     correspond to the <see cref="DeleteEnrollmentGroupAsync(string, string)"/> with the <code>eTag="*"</code>.
        ///
        /// Note that delete the enrollmentGroup will not remove the Devices itself from the IotHub.
        ///
        /// If the enrollmentGroupId does not exists, this method will throw <see cref="ProvisioningServiceClientNotFoundException"/>.
        ///     For more exceptions that this method can throw, please see <see cref="ExceptionHandlingHelper"/>
        /// </remarks>
        /// <param name="enrollmentGroupId">the <code>string</code> that identifies the enrollmentGroup. It cannot be <code>null</code> or empty.</param>
        /// <exception cref="ProvisioningServiceClientException">if the Provisioning Device Service was not able to delete the enrollmentGroup information for the provided enrollmentGroupId.</exception>
        public Task DeleteEnrollmentGroupAsync(string enrollmentGroupId)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_019: [The deleteEnrollmentGroup shall delete the enrollmentGroup for the provided enrollmentGroupId by calling the delete in the EnrollmentGroupManager.] */
            return EnrollmentGroupManager.DeleteAsync(_contractApiHttp, enrollmentGroupId, CancellationToken.None);
        }

        /// <summary>
        /// Delete the enrollmentGroup information.
        /// </summary>
        /// <remarks>
        /// This method will remove the enrollmentGroup from the Device Provisioning Service using the
        ///     provided enrollmentGroupId and eTag. If you want to delete the enrollmentGroup regardless the eTag, you can
        ///     use <see cref="DeleteEnrollmentGroupAsync(string)"/> or you can pass the eTag as <code>null</code>, empty, or
        ///     <code>"*"</code>.
        ///
        /// Note that delete the enrollmentGroup will not remove the Device itself from the IotHub.
        ///
        /// If the enrollmentGroupId does not exists or eTag does not matches, this method will throw
        ///     <see cref="ProvisioningServiceClientNotFoundException"/>. For more exceptions that this method can throw, please see
        ///     <see cref="ExceptionHandlingHelper"/>
        /// </remarks>
        /// <param name="enrollmentGroupId">the <code>string</code> that identifies the enrollmentGroup. It cannot be <code>null</code> or empty.</param>
        /// <param name="eTag">the <code>string</code> with the enrollmentGroup eTag. It can be <code>null</code> or empty.
        ///             The Device Provisioning Service will ignore it in all of these cases.</param>
        /// <exception cref="ProvisioningServiceClientException">if the Provisioning Device Service was not able to delete the enrollmentGroup information for the provided enrollmentGroupId and eTag.</exception>
        public Task DeleteEnrollmentGroupAsync(string enrollmentGroupId, string eTag)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_020: [The deleteEnrollmentGroup shall delete the enrollmentGroup for the provided enrollmentGroupId and eTag by calling the delete in the EnrollmentGroupManager.] */
            return EnrollmentGroupManager.DeleteAsync(_contractApiHttp, enrollmentGroupId, CancellationToken.None, eTag);
        }

        /// <summary>
        /// Factory to create an enrollmentGroup query.
        /// </summary>
        /// <remarks>
        /// This method will create a new enrollment group query on Device Provisioning Service and return it as
        ///     a <see cref="Query"/> iterator.
        ///
        /// The Device Provisioning Service expects a SQL query in the <see cref="QuerySpecification"/>, for instance
        ///     <code>"SELECT * FROM enrollments"</code>.
        /// </remarks>
        /// <param name="querySpecification">the <see cref="QuerySpecification"/> with the SQL query. It cannot be <code>null</code>.</param>
        /// <returns>The <see cref="Query"/> iterator.</returns>
        /// <exception cref="ArgumentException">if the provided parameter is not correct.</exception>
        public Query CreateEnrollmentGroupQuery(QuerySpecification querySpecification)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_021: [The createEnrollmentGroupQuery shall create a new enrolmentGroup query by calling the createQuery in the EnrollmentGroupManager.] */
            return EnrollmentGroupManager.CreateQuery(querySpecification);
        }

        /// <summary>
        /// Factory to create an enrollmentGroup query.
        /// </summary>
        /// <remarks>
        /// This method will create a new enrollment group query on Device Provisioning Service and return it as
        ///     a <see cref="Query"/> iterator.
        ///
        /// The Device Provisioning Service expects a SQL query in the <see cref="QuerySpecification"/>, for instance
        ///     <code>"SELECT * FROM enrollments"</code>.
        ///
        /// For each iteration, the Query will return a List of objects correspondent to the query result. The maximum
        ///     number of items per iteration can be specified by the pageSize. It is optional, you can provide <b>0</b> for
        ///     default pageSize or use the API <see cref="CreateEnrollmentGroupQuery(QuerySpecification)"/>.
        /// </remarks>
        /// <param name="querySpecification">the <see cref="QuerySpecification"/> with the SQL query. It cannot be <code>null</code>.</param>
        /// <param name="pageSize">the <code>int</code> with the maximum number of items per iteration. It can be 0 for default, but not negative.</param>
        /// <returns>The <see cref="Query"/> iterator.</returns>
        /// <exception cref="ArgumentException">if the provided parameters are not correct.</exception>
        public Query CreateEnrollmentGroupQuery(QuerySpecification querySpecification, int pageSize)
        {
            /* SRS_PROVISIONING_SERVICE_CLIENT_21_022: [The createEnrollmentGroupQuery shall create a new enrolmentGroup query by calling the createQuery in the EnrollmentGroupManager.] */
            return EnrollmentGroupManager.CreateQuery(querySpecification, pageSize);
        }

        /// <summary>
        /// Retrieve the registration status information.
        /// </summary>
        /// <remarks>
        /// This method will return the registrationStatus for the provided id. It will retrieve
        ///     the correspondent registrationStatus from the Device Provisioning Service, and return it in the
        ///     <see cref="DeviceRegistrationState"/> object.
        ///
        /// If the id do not exists, this method will throw <see cref="ProvisioningServiceClientNotFoundException"/>.
        ///     For more exceptions that this method can throw, please see <see cref="ExceptionHandlingHelper"/>
        /// </remarks>
        /// <param name="id">the <code>string</code> that identifies the registrationStatus. It cannot be <code>null</code> or empty.</param>
        /// <returns>The <see cref="DeviceRegistrationState"/> with the content of the registrationStatus in the Provisioning Device Service.</returns>
        /// <exception cref="ProvisioningServiceClientException">if the Provisioning Device Service was not able to retrieve the registrationStatus information for the provided registrationId.</exception>
        //public DeviceRegistrationState GetDeviceRegistrationState(string id)
        //{
        //    /* SRS_PROVISIONING_SERVICE_CLIENT_21_023: [The getDeviceRegistrationState shall retrieve the registrationStatus information for the provided id by calling the get in the registrationStatusManager.] */
        //    return RegistrationStatusManager.Get(id);
        //}

        /// <summary>
        /// Delete the Registration Status information.
        /// </summary>
        /// <remarks>
        /// This method will remove the registrationStatus from the Device Provisioning Service using the
        ///     provided <see cref="DeviceRegistrationState"/> information. The Device Provisioning Service will care about the
        ///     id and the eTag on the DeviceRegistrationState. If you want to delete the registrationStatus regardless the
        ///     eTag, you can use the <see cref="DeleteDeviceRegistrationStatusAsync(string)"/> passing only the id.
        ///
        /// If the id does not exists or the eTag does not matches, this method will throw
        ///     <see cref="ProvisioningServiceClientNotFoundException"/>. For more exceptions that this method can throw, please see
        ///     <see cref="ExceptionHandlingHelper"/>
        /// </remarks>
        /// <param name="deviceRegistrationState">the <see cref="DeviceRegistrationState"/> that identifies the registrationStatus. It cannot be <code>null</code>.</param>
        /// <exception cref="ProvisioningServiceClientException">if the Provisioning Device Service was not able to delete the registration status information for the provided DeviceRegistrationState.</exception>
        //public void DeleteDeviceRegistrationStatus(DeviceRegistrationState deviceRegistrationState)
        //{
        //    /* SRS_PROVISIONING_SERVICE_CLIENT_21_024: [The deleteDeviceRegistrationStatus shall delete the registrationStatus for the provided DeviceRegistrationState by calling the delete in the registrationStatusManager.] */
        //    RegistrationStatusManager.Delete(deviceRegistrationState);
        //}

        /// <summary>
        /// Delete the registration status information.
        /// </summary>
        /// <remarks>
        /// This method will remove the registrationStatus from the Device Provisioning Service using the
        ///     provided id. It will delete the registration status regardless the eTag. It means that this API
        ///     correspond to the <see cref="DeleteDeviceRegistrationStatusAsync(string, string)"/> with the <code>eTag="*"</code>.
        ///
        /// If the id does not exists, this method will throw <see cref="ProvisioningServiceClientNotFoundException"/>.
        ///     For more exceptions that this method can throw, please see <see cref="ExceptionHandlingHelper"/>
        /// </remarks>
        /// <param name="id">the <code>string</code> that identifies the registrationStatus. It cannot be <code>null</code> or empty.</param>
        /// <exception cref="ProvisioningServiceClientException">if the Provisioning Device Service was not able to delete the registrationStatus information for the provided registrationId.</exception>
        //public void DeleteDeviceRegistrationStatus(string id)
        //{
        //    /* SRS_PROVISIONING_SERVICE_CLIENT_21_025: [The deleteDeviceRegistrationStatus shall delete the registrationStatus for the provided id by calling the delete in the registrationStatusManager.] */
        //    RegistrationStatusManager.Delete(id);
        //}

        /// <summary>
        /// Delete the registration status information.
        /// </summary>
        /// <remarks>
        /// This method will remove the registration status from the Device Provisioning Service using the
        ///     provided id and eTag. If you want to delete the registration status regardless the eTag, you can
        ///     use <see cref="DeleteDeviceRegistrationStatusAsync(string)"/> or you can pass the eTag as <code>null</code>, empty, or
        ///     <code>"*"</code>.
        ///
        /// If the id does not exists or the eTag does not matches, this method will throw
        ///     <see cref="ProvisioningServiceClientNotFoundException"/>. For more exceptions that this method can throw, please see
        ///     <see cref="ExceptionHandlingHelper"/>
        /// </remarks>
        /// <param name="id">the <code>string</code> that identifies the registrationStatus. It cannot be <code>null</code> or empty.</param>
        /// <param name="eTag">the <code>string</code> with the registrationStatus eTag. It can be <code>null</code> or empty.
        ///             The Device Provisioning Service will ignore it in all of these cases.</param>
        /// <exception cref="ProvisioningServiceClientException">if the Provisioning Device Service was not able to delete the registrationStatus information for the provided registrationId and eTag.</exception>
        //public void DeleteDeviceRegistrationStatus(string id, string eTag)
        //{
        //    /* SRS_PROVISIONING_SERVICE_CLIENT_21_026: [The deleteDeviceRegistrationStatus shall delete the registrationStatus for the provided id and eTag by calling the delete in the registrationStatusManager.] */
        //    RegistrationStatusManager.Delete(id, eTag);
        //}

        /// <summary>
        /// Factory to create a registration status query.
        /// </summary>
        /// <remarks>
        /// This method will create a new registration status query for a specific enrollment group on the Device
        ///     Provisioning Service and return it as a <see cref="Query"/> iterator.
        ///
        /// The Device Provisioning Service expects a SQL query in the <see cref="QuerySpecification"/>, for instance
        ///     <code>"SELECT * FROM enrollments"</code>.
        /// </remarks>
        /// <param name="querySpecification">the <see cref="QuerySpecification"/> with the SQL query. It cannot be <code>null</code>.</param>
        /// <param name="enrollmentGroupId">the <code>string</code> that identifies the enrollmentGroup. It cannot be <code>null</code> or empty.</param>
        /// <returns>The <see cref="Query"/> iterator.</returns>
        //public Query CreateEnrollmentGroupRegistrationStatusQuery(QuerySpecification querySpecification, string enrollmentGroupId)
        //{
        //    /* SRS_PROVISIONING_SERVICE_CLIENT_21_027: [The createEnrollmentGroupRegistrationStatusQuery shall create a new registrationStatus query by calling the createQuery in the registrationStatusManager.] */
        //    return RegistrationStatusManager.CreateEnrollmentGroupQuery(querySpecification, enrollmentGroupId);
        //}

        /// <summary>
        /// Factory to create a registration status query.
        /// </summary>
        /// <remarks>
        /// This method will create a new registration status query for a specific enrollment group on the Device
        ///     Provisioning Service and return it as a <see cref="Query"/> iterator.
        ///
        /// The Device Provisioning Service expects a SQL query in the <see cref="QuerySpecification"/>, for instance
        ///     <code>"SELECT * FROM enrollments"</code>.
        ///
        /// For each iteration, the Query will return a List of objects correspondent to the query result. The maximum
        ///     number of items per iteration can be specified by the pageSize. It is optional, you can provide <b>0</b> for
        ///     default pageSize or use the API <see cref="CreateIndividualEnrollmentQuery(QuerySpecification)"/>.
        /// </remarks>
        /// <param name="querySpecification">the <see cref="QuerySpecification"/> with the SQL query. It cannot be <code>null</code>.</param>
        /// <param name="enrollmentGroupId">the <code>string</code> that identifies the enrollmentGroup. It cannot be <code>null</code> or empty.</param>
        /// <param name="pageSize">the <code>int</code> with the maximum number of items per iteration. It can be 0 for default, but not negative.</param>
        /// <returns>The <see cref="Query"/> iterator.</returns>
        /// <exception cref="ArgumentException">if the provided parameters are not correct.</exception>
        //public Query CreateEnrollmentGroupRegistrationStatusQuery(QuerySpecification querySpecification, string enrollmentGroupId, int pageSize)
        //{
        //    /* SRS_PROVISIONING_SERVICE_CLIENT_21_028: [The createEnrollmentGroupRegistrationStatusQuery shall create a new registrationStatus query by calling the createQuery in the registrationStatusManager.] */
        //    return RegistrationStatusManager.CreateEnrollmentGroupQuery(querySpecification, enrollmentGroupId, pageSize);
        //}
    }
}
