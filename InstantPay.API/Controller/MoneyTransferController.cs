using InstantPay.Application.Interfaces;
using InstantPay.Application.Interfaces.MoneyTransfer.Castler;
using InstantPay.Application.Interfaces.MoneyTransfer.AeronPay;
using InstantPay.Application.Interfaces.MoneyTransfer.Finzep;
using InstantPay.Application.Interfaces.MoneyTransfer.NIFI;
using InstantPay.Application.Interfaces.MoneyTransfer.RechargeKit;
using InstantPay.Application.Interfaces.MoneyTransfer.Tramo;
using InstantPay.Application.Interfaces.MoneyTransfer.RBL;
using InstantPay.Application.Services;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.RequestPayload;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.Castler;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.AeronPay;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.Finzep;
using InstantPay.SharedKernel.RequestPayload.MoneyTransfer.NIFI;
using InstantPay.SharedKernel.Results;
using InstantPay.SharedKernel.Results.MoneyTransfer.Castler;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstantPay.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class MoneyTransferController : ControllerBase
    {
        private readonly ICastlerDmtService _dmtService;
        private readonly INifiDmtService _nifiDmtService;
        private readonly IFinzepDmtService _finzepDmtService;
        private readonly IAeronpayDmtService _aeronpayDmtService;
        private readonly IRechargeKitDmtService _rechargeKitDmtService;
        private readonly ITramoUpiDmtService _tramoDmtService;
        private readonly IRblDmtService _rblDmtService;
        private readonly AppDbContext _context;

        public MoneyTransferController(ICastlerDmtService dmtService, INifiDmtService nifiDmtService, IFinzepDmtService finzepDmtService, IAeronpayDmtService aeronpayDmtService, IRechargeKitDmtService rechargeKitDmtService, ITramoUpiDmtService tramoDmtService, IRblDmtService rblDmtService, AppDbContext context)
        {
            _dmtService = dmtService;
            _nifiDmtService = nifiDmtService;
            _finzepDmtService = finzepDmtService;
            _aeronpayDmtService = aeronpayDmtService;
            _rechargeKitDmtService = rechargeKitDmtService;
            _tramoDmtService = tramoDmtService;
            _rblDmtService = rblDmtService;
            _context = context;
        }


        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer([FromBody] DmtRequest model, CancellationToken cancellationToken)
        {
            try
            {
                string ip = HttpContext.Connection.RemoteIpAddress?.ToString()?? "103.49.124.63";

                var result = await _dmtService.MoneyTransfer(model, ip, cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }

        [HttpGet("status/{txnId}")]
        public async Task<IActionResult> CheckStatus(string txnId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _dmtService.CheckStatus(txnId, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }

        [HttpPost("nifi/transfer")]
        public async Task<IActionResult> NifiTransfer([FromBody] NifiDmtRequest model, CancellationToken cancellationToken)
        {
            try
            {
                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "103.49.124.63";
                string requestJson = JsonConvert.SerializeObject(model);

                var result = await _nifiDmtService.MoneyTransfer(model, ip, cancellationToken);
                string responseJson = JsonConvert.SerializeObject(result);

                // Log to apilogs table
                var log = new Apilog
                {
                    Apiname = "NIFI-Transfer",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }

        [HttpGet("nifi/status/{txnId}")]
        public async Task<IActionResult> NifiCheckStatus(string txnId, CancellationToken cancellationToken)
        {
            try
            {
                string requestJson = txnId;

                var result = await _nifiDmtService.CheckStatus(txnId, cancellationToken);
                string responseJson = JsonConvert.SerializeObject(result);

                // Log to apilogs table
                var log = new Apilog
                {
                    Apiname = "NIFI-CheckStatus",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }

        [HttpPost("fzp/transfer")]
        public async Task<IActionResult> FinzepTransfer([FromBody] FinzepDmtRequest model, CancellationToken cancellationToken)
        {
            try
            {
                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "103.49.124.63";
                string requestJson = JsonConvert.SerializeObject(model);

                var result = await _finzepDmtService.MoneyTransfer(model, ip, cancellationToken);
                string responseJson = JsonConvert.SerializeObject(result);

                // Log to apilogs table
                var log = new Apilog
                {
                    Apiname = "FZP-Transfer",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }

        [HttpGet("fzp/status/{txnId}")]
        public async Task<IActionResult> FinzepCheckStatus(string txnId, CancellationToken cancellationToken)
        {
            try
            {
                string requestJson = txnId;

                var result = await _finzepDmtService.CheckStatus(txnId, cancellationToken);
                string responseJson = JsonConvert.SerializeObject(result);

                // Log to apilogs table
                var log = new Apilog
                {
                    Apiname = "FZP-CheckStatus",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }

        [HttpPost("arp/transfer")]
        public async Task<IActionResult> AeronpayTransfer([FromBody] AeronpayDmtRequest model, CancellationToken cancellationToken)
        {
            try
            {
                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "103.49.124.63";
                string requestJson = JsonConvert.SerializeObject(model);

                var result = await _aeronpayDmtService.MoneyTransfer(model, ip, cancellationToken);
                string responseJson = JsonConvert.SerializeObject(result);

                // Log to apilogs table
                var log = new Apilog
                {
                    Apiname = "ARP-Transfer",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }

        [HttpGet("arp/status/{txnId}")]
        public async Task<IActionResult> AeronpayCheckStatus(string txnId, CancellationToken cancellationToken)
        {
            try
            {
                string requestJson = txnId;

                var result = await _aeronpayDmtService.CheckStatus(txnId, cancellationToken);
                string responseJson = JsonConvert.SerializeObject(result);

                // Log to apilogs table
                var log = new Apilog
                {
                    Apiname = "ARP-CheckStatus",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }

        [HttpPost("rkit/transfer")]
        public async Task<IActionResult> RechargeKitTransfer([FromBody] AeronpayDmtRequest model, CancellationToken cancellationToken)
        {
            try
            {
                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "103.49.124.63";
                string requestJson = JsonConvert.SerializeObject(model);

                var result = await _rechargeKitDmtService.MoneyTransfer(model, ip, cancellationToken);
                string responseJson = JsonConvert.SerializeObject(result);

                // Log to apilogs table
                var log = new Apilog
                {
                    Apiname = "RKIT-Transfer",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }

        [HttpPost("tramo/transfer")]
        public async Task<IActionResult> TramoTransfer([FromBody] AeronpayDmtRequest model, CancellationToken cancellationToken)
        {
            try
            {
                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "103.49.124.63";
                string requestJson = JsonConvert.SerializeObject(model);

                var result = await _tramoDmtService.MoneyTransfer(model, ip, cancellationToken);
                string responseJson = JsonConvert.SerializeObject(result);

                var log = new Apilog
                {
                    Apiname = "TRAMO-Transfer",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }

        [HttpPost("rbl/transfer")]
        public async Task<IActionResult> RblTransfer([FromBody] AeronpayDmtRequest model, CancellationToken cancellationToken)
        {
            string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "103.49.124.63";
            var result = await _rblDmtService.MoneyTransfer(model, ip, cancellationToken);
            return Ok(result);
        }

        [HttpGet("tramo/status/{txnId}")]
        public async Task<IActionResult> TramoCheckStatus(string txnId, CancellationToken cancellationToken)
        {
            try
            {
                string requestJson = txnId;

                var result = await _tramoDmtService.CheckStatus(txnId, cancellationToken);
                string responseJson = JsonConvert.SerializeObject(result);

                var log = new Apilog
                {
                    Apiname = "TRAMO-CheckStatus",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }

        [HttpGet("rkit/status/{txnId}")]
        public async Task<IActionResult> RechargeKitCheckStatus(string txnId, CancellationToken cancellationToken)
        {
            try
            {
                string requestJson = txnId;

                var result = await _rechargeKitDmtService.CheckStatus(txnId, cancellationToken);
                string responseJson = JsonConvert.SerializeObject(result);

                // Log to apilogs table
                var log = new Apilog
                {
                    Apiname = "RKIT-CheckStatus",
                    Reqdatae = DateTime.Now,
                    Request = requestJson,
                    Response = responseJson
                };
                _context.Apilogs.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new LoginModel
                {
                    Status_Code = "0",
                    Message = "ERR:500 " + ex.Message,
                    Data = null
                });
            }
        }
    }
}
