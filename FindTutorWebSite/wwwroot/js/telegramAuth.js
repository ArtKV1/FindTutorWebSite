window.onload = function ()
{
    $.ajax({
        type: "POST",
        url: "/Auth/CheckAuth",
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response)
        {
            if (response.auth)
            {
                window.location.href = "/Home/FindTutor";
            }
            else
            {
                console.log("Error");
            }
        }
    });
}


function onTelegramAuth(user) {
    var dataToSend = {
        auth_date: user.auth_date,
        first_name: user.first_name,
        id: user.id,
        username: user.username,
        hash: user.hash,
        last_name: user.last_name,
        photo_url: user.photo_url
    };

    console.log(user);

    $.ajax({
        type: "POST",
        url: "/Auth/Login", // Путь к вашему методу контроллера для сохранения данных
        data: JSON.stringify(dataToSend),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (response)
        {
            if (response.auth)
            {
                window.location.href = "/Home/FindTutor";
            }
            else
            {
                console.log("Error");
            }
        }
    });
  }