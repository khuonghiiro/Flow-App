// Nhập code JavaScript. Biến từ input dùng tên key (ví dụ: jsonData).
// return { key1: value1, key2: value2 }; để trả về outputs.
// 
// Script này normalize JSON không chuẩn và transform thành 2 format cho MediaGallery:
// - gridJson: Array flat cho chế độ "Ảnh/Video theo lưới"
// - groupedJson: Structure theo workflows cho chế độ "Ảnh/Video theo nhóm"

function normalizeJsonString(jsonStr) {
    if (!jsonStr || typeof jsonStr !== 'string') return jsonStr;
    
    // Thử parse trước, nếu đã hợp lệ thì return luôn
    try {
        JSON.parse(jsonStr);
        return jsonStr;
    } catch (e) {
        // Không hợp lệ, cần normalize
    }
    
    // Normalize: thêm dấu ngoặc kép cho key không có dấu ngoặc
    // Pattern: tìm key không có dấu ngoặc (word character) theo sau bởi dấu :
    let normalized = jsonStr.trim();
    
    // Thay thế key không có dấu ngoặc bằng key có dấu ngoặc
    // Ví dụ: {media: → {"media":
    normalized = normalized.replace(/([{,]\s*)([a-zA-Z_$][a-zA-Z0-9_$]*)\s*:/g, '$1"$2":');
    
    return normalized;
}

function transformToGridFormat(data) {
    if (!data || !data.media || !Array.isArray(data.media)) {
        return [];
    }
    
    return data.media.map(item => {
        // Lấy title từ name hoặc prompt
        let title = item.name || '';
        if (item.image && item.image.generatedImage && item.image.generatedImage.prompt) {
            title = item.image.generatedImage.prompt;
        }
        
        // Lấy fifeUrl từ image.generatedImage.fifeUrl
        let fifeUrl = '';
        if (item.image && item.image.generatedImage && item.image.generatedImage.fifeUrl) {
            fifeUrl = item.image.generatedImage.fifeUrl;
        }
        
        // Lấy videoUrl nếu có (tương tự)
        let videoUrl = '';
        if (item.video && item.video.generatedVideo && item.video.generatedVideo.fifeUrl) {
            videoUrl = item.video.generatedVideo.fifeUrl;
        }
        
        return {
            name: title,
            fifeUrl: fifeUrl,
            videoUrl: videoUrl || null,
            workflowId: item.workflowId || null
        };
    }).filter(item => item.fifeUrl || item.videoUrl); // Chỉ lấy item có ít nhất 1 URL
}

function transformToGroupedFormat(data) {
    if (!data || !data.workflows || !Array.isArray(data.workflows)) {
        return { workflows: [] };
    }
    
    if (!data.media || !Array.isArray(data.media)) {
        return { workflows: [] };
    }
    
    // Tạo map media theo workflowId để lookup nhanh
    const mediaByWorkflowId = {};
    data.media.forEach(item => {
        const workflowId = item.workflowId;
        if (!workflowId) return;
        
        if (!mediaByWorkflowId[workflowId]) {
            mediaByWorkflowId[workflowId] = [];
        }
        
        // Lấy title từ name hoặc prompt
        let title = item.name || '';
        if (item.image && item.image.generatedImage && item.image.generatedImage.prompt) {
            title = item.image.generatedImage.prompt;
        }
        
        // Lấy fifeUrl
        let fifeUrl = '';
        if (item.image && item.image.generatedImage && item.image.generatedImage.fifeUrl) {
            fifeUrl = item.image.generatedImage.fifeUrl;
        }
        
        // Lấy videoUrl nếu có
        let videoUrl = '';
        if (item.video && item.video.generatedVideo && item.video.generatedVideo.fifeUrl) {
            videoUrl = item.video.generatedVideo.fifeUrl;
        }
        
        if (fifeUrl || videoUrl) {
            mediaByWorkflowId[workflowId].push({
                name: title,
                fifeUrl: fifeUrl,
                videoUrl: videoUrl || null
            });
        }
    });
    
    // Transform workflows thành format grouped
    const workflows = data.workflows.map(workflow => {
        const workflowId = workflow.name || workflow.workflowId;
        const videos = mediaByWorkflowId[workflowId] || [];
        
        return {
            workflowId: workflowId,
            videos: videos
        };
    }).filter(workflow => workflow.videos.length > 0); // Chỉ giữ workflow có videos
    
    return { workflows: workflows };
}

function main() {
    try {
        // Input từ node trước đó - có thể là string hoặc object đã parse
        // CodeNodeExecutor sẽ parse JSON string thành object nếu có thể
        // Thử nhiều cách để lấy input
        let rawData = null;
        
        // Thử các biến có thể có từ InputMapping
        if (typeof rawJson !== 'undefined') {
            rawData = rawJson;
        } else if (typeof jsonData !== 'undefined') {
            rawData = jsonData;
        } else if (typeof input !== 'undefined') {
            rawData = input;
        } else if (typeof data !== 'undefined') {
            rawData = data;
        }
        
        // Nếu không tìm thấy, thử lấy từ arguments hoặc global
        if (rawData === null || rawData === undefined) {
            // Nếu input là object có nested properties
            if (typeof input !== 'undefined' && input !== null) {
                if (input.result) rawData = input.result;
                else if (input.data) rawData = input.data;
                else if (input.json) rawData = input.json;
                else rawData = input;
            }
        }
        
        // Nếu vẫn null, return error
        if (rawData === null || rawData === undefined) {
            return {
                gridJson: JSON.stringify([]),
                groupedJson: JSON.stringify({ workflows: [] }),
                error: 'Không tìm thấy input. Hãy đảm bảo có Input Mapping với key: rawJson, jsonData, hoặc input'
            };
        }
        
        // Nếu là string thì cần normalize và parse
        if (typeof rawData === 'string') {
            const normalizedJsonStr = normalizeJsonString(rawData);
            
            try {
                rawData = JSON.parse(normalizedJsonStr);
            } catch (parseError) {
                // Nếu vẫn không parse được, thử eval (fallback)
                try {
                    rawData = eval('(' + normalizedJsonStr + ')');
                } catch (evalError) {
                    return {
                        gridJson: JSON.stringify([]),
                        groupedJson: JSON.stringify({ workflows: [] }),
                        error: 'Không thể parse JSON: ' + parseError.message
                    };
                }
            }
        }
        
        // Kiểm tra rawData có phải object hợp lệ không
        if (!rawData || typeof rawData !== 'object') {
            return {
                gridJson: JSON.stringify([]),
                groupedJson: JSON.stringify({ workflows: [] }),
                error: 'Input không phải là object hợp lệ. Type: ' + typeof rawData
            };
        }
        
        // Transform thành 2 format
        const gridItems = transformToGridFormat(rawData);
        const groupedData = transformToGroupedFormat(rawData);
        
        // Return outputs
        return {
            gridJson: JSON.stringify(gridItems),
            groupedJson: JSON.stringify(groupedData),
            debug: {
                inputType: typeof rawData,
                hasMedia: !!(rawData.media && Array.isArray(rawData.media)),
                mediaCount: rawData.media ? rawData.media.length : 0,
                hasWorkflows: !!(rawData.workflows && Array.isArray(rawData.workflows)),
                workflowsCount: rawData.workflows ? rawData.workflows.length : 0,
                gridItemsCount: gridItems.length,
                groupedWorkflowsCount: groupedData.workflows ? groupedData.workflows.length : 0
            }
        };
        
    } catch (error) {
        return {
            gridJson: JSON.stringify([]),
            groupedJson: JSON.stringify({ workflows: [] }),
            error: error.message || String(error),
            stack: error.stack
        };
    }
}

return main();
