const fs = require('fs');
const glob = require('glob');
const axios = require('axios');
const { exec } = require('child_process');
const path = require('path');
const FormData1 = require('form-data');
const {
    v1: uuidv1,
    v4: uuidv4,
} = require('uuid');
var Prompt = require('prompt-checkbox');

const proxy = undefined;/*{
protocol: 'http',
host: '192.168.2.100',
port: 1212,
};*/

console.log(`Запуск с аргументами: ${process.argv.join(', ')}`);

// Получение аргументов
const [, , apiUrl, apiKey] = process.argv;

console.log(`API URL: ${apiUrl}`);
console.log(`API Key: ${apiKey}`);

if (!apiUrl || !apiKey) {
    console.error("Укажите apiUrl и apiKey в качестве аргументов. например http://192.168.2.75:3123/api/map debug");
    process.exit(1);
}

process.chdir('../../../');

function fixExtent(ext) {
    return { "A": { "X": ext.X1 || 0, "Y": ext.Y1 || 0 }, "B": { "X": ext.X2 || 0, "Y": ext.Y2 || 0 } };
}

// Функция для поиска всех .yml файлов по маске
function getYamlFiles() {
    const patterns = [
        'Resources/Maps/*.yml',          // файлы в Resources/Maps
        'Resources/Maps/Backmen/*.yml'   // файлы в Resources/Maps/Backmen
    ];

    // Используем glob для поиска файлов по маскам
    let fileList = [];
    patterns.forEach(pattern => {
        const files = glob.sync(pattern);
        fileList = fileList.concat(files.map(file => path.basename(file)));
    });

    return fileList;
}

async function runDotnet(fileList) {
    return new Promise((resolve, reject) => {
        const command = `dotnet run -c Release --project Content.MapRenderer -- --format webp --viewer -f ${fileList.join(' ')}`;
        const proc = exec(command, { cwd: process.cwd(), encoding: 'utf8' }, (error, stdout, stderr) => {
            if (error) {
                return reject(`Ошибка при выполнении: ${stderr}`);
            }
            resolve(stdout);
        });

        // Выводим процесс в реальном времени
        proc.stdout.on('data', (data) => process.stdout.write(data));
        proc.stderr.on('data', (data) => process.stderr.write(data));
    });
}

async function checkIfMapExists(mapId) {
    try {
        let response = await axios.get(apiUrl, { proxy: proxy });
        let maps = response.data;
        let result = maps.filter(map => String(map.mapId).toLowerCase() === mapId.toLowerCase());
        return result.length > 0 ? result[0] : undefined;
    } catch (error) {
        console.error("Ошибка при получении списка карт:", error);
        return undefined;
    }
}

async function uploadMapData(mapData, images, method = 'POST') {
    mapData.DisplayName = mapData.DisplayName || mapData.Name;
    mapData.GitRef = mapData.GitRef || "master";
    mapData.MapId = String(mapData.Id).toLowerCase();

    var files = {};

    for (let grid of mapData.Grids) {
        files[path.basename(grid.Url)] = grid.GridId;
        grid.Path = "data/" + mapData.GitRef + "/grid_images/" + mapData.MapId + "/" + grid.GridId + ".webp";
        grid.Id = uuidv4();
        grid.Tiled = true;
        grid.Extent = fixExtent(grid.Extent);
    }
    for (let paralax of mapData.ParallaxLayers) {
        if (paralax.Source && paralax.Source.Extent) {
            paralax.Source.Extent = fixExtent(paralax.Source.Extent);
        }
    }

    const formData = new FormData1();
    formData.append('map', JSON.stringify(mapData));

    console.log(`Загружаем карту: ${mapData.DisplayName} ${mapData.MapGuid}`);
    if (method != "POST" && !mapData.MapGuid) {
        throw new Error("Не указан Guid карты");
    }
    images.forEach((file) => {
        const fileName = path.basename(file);
        formData.append('images', fs.createReadStream(file), files[fileName] + '.webp');
    });

    try {
        const response = await axios({
            method,
            url: `${apiUrl}/${method === 'PUT' ? `${mapData.Id}/${mapData.GitRef}` : ''}`,
            data: formData,
            headers: {
                'X-API-Key': apiKey,
                ...formData.getHeaders()
            },
            proxy: proxy
        });
        console.log(`Карта успешно загружена: ${response.status}`);
    } catch (error) {
        console.error(`Ошибка при загрузке карты: ${error}`);
        //throw new Error(`Ошибка при загрузке карты: ${error}`);
    }
}

// Основная логика
async function processMaps() {
    const mapImageDir = path.join(process.cwd(), 'Resources', 'MapImages');
    const directories = fs.readdirSync(mapImageDir).filter(file => fs.lstatSync(path.join(mapImageDir, file)).isDirectory());

    for (const dir of directories) {
        const mapJsonPath = path.join(mapImageDir, dir, 'map.json');
        if (!fs.existsSync(mapJsonPath)) {
            console.error(`Файл map.json не найден в папке ${dir}`);
            continue;
        }

        const mapData = JSON.parse(fs.readFileSync(mapJsonPath, 'utf-8'));
        mapData.Id = mapData.Id.toLowerCase();
        const webpFiles = fs.readdirSync(path.join(mapImageDir, dir)).filter(file => file.endsWith('.webp'));

        const mapExists = await checkIfMapExists(mapData.Id);
        const method = mapExists ? 'PUT' : 'POST';
        if (mapExists) {
            console.log(mapExists);
            mapData.MapGuid = mapExists.mapGuid;
        }

        await uploadMapData(mapData, webpFiles.map(file => path.join(mapImageDir, dir, file)), method);
        fs.rmdirSync(path.join(mapImageDir, dir), { recursive: true, force: true });
    }
}

// Функция для разбиения массива на батчи (по 3 или 4 элемента)
function chunkArray(array, chunkSize) {
    let chunks = [];
    for (let i = 0; i < array.length; i += chunkSize) {
        chunks.push(array.slice(i, i + chunkSize));
    }
    return chunks;
}

// Выполняем основной процесс
(async () => {
    console.log('Запуск основного процесса');
    try {
        // Автоматическое получение списка .yml файлов по маске
        let files = getYamlFiles();

        var prompt = new Prompt({
            name: 'render',
            message: 'Какие карты нужно обновить?',
            radio: true,
            choices: files
        });
        let filesToRun = await prompt.run();
        const fileBatches = chunkArray(filesToRun, 2);

        console.log('Запуск обработки файлами батчами');
        for (const batch of fileBatches) {
            console.log(`Запуск runDotnet для батча: ${batch}`);
            const result = await runDotnet(batch);


            if (result.includes("It's now safe to manually exit the process")) {
                await processMaps();
            } else {
                throw new Error("Что-то пошло не так");
            }
        }
        // await processMaps();
    } catch (error) {
        console.error(error);
    }
})();
