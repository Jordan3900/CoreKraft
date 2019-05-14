function FirstExampleApp() {
    AppBase.apply(this, arguments);
    this.setFinalAuthority(true);
}

FirstExampleApp.Inherit(AppBase, "FirstExampleApp");
FirstExampleApp.Implement(IPlatformUtilityImpl, "FirstExample");

FirstExampleApp.prototype.provideAsServices = ["FirstExampleApp"];
FirstExampleApp.prototype.get_caption = function () {
    return "FirstExampleApp";
}

FirstExampleApp.registerShellCommand("FirstExampleApp", "callook", function (args) {
    Shell.launchApp("FirstExampleApp");
}, "Ultimate");

FirstExampleApp.prototype.appinitialize = function (callback, args) {
    var singleWnd = new SimpleViewWindow(
        WindowStyleFlags.visible | WindowStyleFlags.parentnotify | WindowStyleFlags.adjustclient | WindowStyleFlags.sizable,
        //this,
        new TemplateConnector("bindkraftstyles/window-mainwindow"),
        new Rect(200, 200, 800, 600),
        {
            url: this.moduleUrl("read", "main", "student")
        });

    this.placeWindow(singleWnd);

    this.mainWindow = singleWnd;
    return true;
}
FirstExampleApp.prototype.run = function () { };
FirstExampleApp.prototype.appshutdown = function () {
    jbTrace.log("The ModularExampleApp is shutting down");
    AppBase.prototype.appshutdown.apply(this, arguments);
}