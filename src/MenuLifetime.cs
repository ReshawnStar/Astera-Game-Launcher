using System;
namespace Astera {
sealed partial class LibraryForm {
    bool releasingMenus;
    protected override void Dispose(bool disposing){if(disposing&&!releasingMenus){releasingMenus=true;if(moreMenu!=null)moreMenu.Close();if(addSourceMenu!=null)addSourceMenu.Close();if(tray!=null){tray.Visible=false;tray.ContextMenuStrip=null;tray.Dispose();}menuComponents.Dispose();}base.Dispose(disposing);}
}
}