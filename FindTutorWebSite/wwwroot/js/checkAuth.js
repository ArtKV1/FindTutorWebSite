window.onload = function () {
    $.ajax({
        type: "POST",
        url: "/Auth/CheckAuth",
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response) {
            if (response.auth) {
            }
            else {
                window.location.href = "/Home/Index";
            }
        }
    });
}