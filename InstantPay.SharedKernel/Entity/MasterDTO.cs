using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstantPay.SharedKernel.Entity
{
    public class ServiceMasterDTO
    {
        
        public List<Service> services { get; set; } = new();
        public decimal walletAmount { get; set; } = new();
        public int totalUserJoined { get; set; } = new();
        public int totalTransection { get; set; } = new();
        public List<int> pieData { get; set; }  
        public List<int> lineData { get; set; }  
        public List<string> lineLabels { get; set; }

    }

    public class Service
    {
        public string ServiceName { get; set; } = string.Empty;
        public int ServiceId { get; set; }
    }

    public class UserMasterDataForDD
    {
        public int Id { get; set; } = 0;
        public string Name { get; set; }
    }

    public class ServiceStatusResponse
    {
        
        public bool UserServiceActive { get; set; }
        public bool ServiceActive { get; set; }
    }

}
